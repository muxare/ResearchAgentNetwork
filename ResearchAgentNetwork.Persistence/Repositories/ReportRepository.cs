using Microsoft.EntityFrameworkCore;
using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence.Repositories;

public class ReportRepository : IReportRepository
{
    private readonly AppDbContext _db;
    public ReportRepository(AppDbContext db) { _db = db; }

    public async Task UpsertReportAsync(Guid taskId, string reportMarkdown, DateTime generatedAtUtc, CancellationToken cancellationToken = default)
    {
        var existing = await _db.TaskReports.FindAsync([taskId], cancellationToken);
        if (existing is null)
        {
            _db.TaskReports.Add(new TaskReportEntity { TaskId = taskId, ReportMarkdown = reportMarkdown, GeneratedAtUtc = generatedAtUtc });
        }
        else
        {
            existing.ReportMarkdown = reportMarkdown;
            existing.GeneratedAtUtc = generatedAtUtc;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskReportEntity?> GetAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        return await _db.TaskReports.FindAsync([taskId], cancellationToken);
    }
}

