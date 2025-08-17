using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public interface IReportRepository
{
    Task UpsertReportAsync(Guid taskId, string reportMarkdown, DateTime generatedAtUtc, CancellationToken cancellationToken = default);
    Task<TaskReportEntity?> GetAsync(Guid taskId, CancellationToken cancellationToken = default);
}

