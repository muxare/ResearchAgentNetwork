namespace ResearchAgentNetwork.Persistence.Entities;

public class TaskEntity
{
    public Guid Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? ParentTaskId { get; set; }
}

public class TaskEventEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string? AgentRole { get; set; }
    public string? DetailsJson { get; set; }
}

public class TaskReportEntity
{
    public Guid TaskId { get; set; }
    public string ReportMarkdown { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
}

