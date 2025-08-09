namespace ResearchAgentNetwork;

public class ResearchResult
{
    public string Content { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public List<string> Sources { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool RequiresAdditionalResearch { get; set; }
}