using Microsoft.EntityFrameworkCore;
using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public class EventRepository : IEventRepository
{
    private readonly AppDbContext _db;
    public EventRepository(AppDbContext db) { _db = db; }

    public async Task AddEventAsync(TaskEventEntity e, CancellationToken cancellationToken = default)
    {
        _db.TaskEvents.Add(e);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskEventEntity>> GetEventsAsync(IEnumerable<Guid> taskIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        var idSet = taskIds.ToHashSet();
        return await _db.TaskEvents
            .Where(ev => idSet.Contains(ev.TaskId))
            .OrderBy(ev => ev.TimestampUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}

