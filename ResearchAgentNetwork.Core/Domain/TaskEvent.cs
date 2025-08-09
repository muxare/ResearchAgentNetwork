namespace ResearchAgentNetwork;

public class TaskEvent
{
    public Guid TaskId { get; set; }
    public TaskStatus Status { get; set; }
    public string EventType { get; set; } = string.Empty; // submitted, status, decomposed, aggregated, completed, failed
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string? Message { get; set; }
    public Guid? ParentTaskId { get; set; }
}