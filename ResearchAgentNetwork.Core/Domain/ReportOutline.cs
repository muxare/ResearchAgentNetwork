namespace ResearchAgentNetwork;

public class ReportOutline
{
    public Guid RootTaskId { get; set; }
    public List<OutlineSection> Sections { get; set; } = new();
}

public class OutlineSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public List<string> EvidenceIds { get; set; } = new();
    public List<string> AcceptanceCriteria { get; set; } = new();
}

