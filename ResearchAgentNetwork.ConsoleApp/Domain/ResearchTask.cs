namespace ResearchAgentNetwork;

public class ResearchTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Description { get; set; } = string.Empty;
    public TaskStatus Status { get; set; } = TaskStatus.Pending;
    public int Priority { get; set; }
    public Guid? ParentTaskId { get; set; }
    public List<Guid> SubTaskIds { get; set; } = new();
    public ResearchResult? Result { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}