using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public interface ITaskRepository
{
    Task UpsertTaskSnapshotAsync(ResearchTask task, CancellationToken cancellationToken = default);
    Task<TaskEntity?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetChildrenIdsAsync(Guid parentId, CancellationToken cancellationToken = default);
}

