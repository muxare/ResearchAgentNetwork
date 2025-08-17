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
        var entity = await _db.Tasks.FindAsync([task.Id], cancellationToken);
        if (entity is null)
        {
            _db.Tasks.Add(new TaskEntity
            {
                Id = task.Id,
                Description = task.Description,
                Priority = task.Priority,
                Status = task.Status.ToString(),
                CreatedAtUtc = task.CreatedAt,
                UpdatedAtUtc = DateTime.UtcNow,
                ParentTaskId = task.ParentTaskId
            });
        }
        else
        {
            entity.Description = task.Description;
            entity.Priority = task.Priority;
            entity.Status = task.Status.ToString();
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.ParentTaskId = task.ParentTaskId;
        }
        await _db.SaveChangesAsync(cancellationToken);
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
}

