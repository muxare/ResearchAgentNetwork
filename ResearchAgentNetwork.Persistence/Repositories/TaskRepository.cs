using Microsoft.EntityFrameworkCore;
using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _db;

    public TaskRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpsertTaskSnapshotAsync(ResearchTask task, CancellationToken cancellationToken = default)
    {
        // Use AsNoTracking to avoid change tracker conflicts in concurrent scenarios
        var entity = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);

        var taskEntity = new TaskEntity
        {
            Id = task.Id,
            Description = task.Description,
            Priority = task.Priority,
            Status = task.Status.ToString(),
            CreatedAtUtc = entity?.CreatedAtUtc ?? task.CreatedAt,
            UpdatedAtUtc = DateTime.UtcNow,
            ParentTaskId = task.ParentTaskId
        };

        if (entity is null)
        {
            _db.Tasks.Add(taskEntity);
        }
        else
        {
            _db.Tasks.Update(taskEntity);
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsPrimaryKeyViolation(ex))
        {
            // PRIMARY KEY violation - entity was inserted by another thread, retry as update
            _db.Entry(taskEntity).State = EntityState.Detached;

            // Re-fetch to get the actual CreatedAtUtc from DB
            var existing = await _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == task.Id, cancellationToken);
            taskEntity.CreatedAtUtc = existing?.CreatedAtUtc ?? task.CreatedAt;

            _db.Tasks.Update(taskEntity);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool IsPrimaryKeyViolation(DbUpdateException ex)
    {
        return ex.InnerException switch
        {
            Microsoft.Data.SqlClient.SqlException sqlEx when sqlEx.Number == 2627 => true,
            Microsoft.Data.Sqlite.SqliteException sqliteEx when sqliteEx.SqliteErrorCode == 19 => true,
            _ => false
        };
    }

    public async Task<TaskEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Tasks.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetChildrenIdsAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        return await _db.Tasks
            .Where(t => t.ParentTaskId == parentId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskEntity>> QueryTasksAsync(string? status, string? searchTerm, DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _db.Tasks.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.ToLower();
            query = query.Where(t => t.Description.ToLower().Contains(term) || t.Id.ToString().ToLower().Contains(term));
        }
        if (fromUtc.HasValue) query = query.Where(t => t.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(t => t.CreatedAtUtc <= toUtc.Value);
        return await query.OrderByDescending(t => t.CreatedAtUtc).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskEntity>> GetRootTasksAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        return await _db.Tasks
            .Where(t => t.ParentTaskId == null)
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}

