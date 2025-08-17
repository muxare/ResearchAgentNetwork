using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public interface IEventRepository
{
    Task AddEventAsync(TaskEventEntity e, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskEventEntity>> GetEventsAsync(IEnumerable<Guid> taskIds, int skip, int take, CancellationToken cancellationToken = default);
}

