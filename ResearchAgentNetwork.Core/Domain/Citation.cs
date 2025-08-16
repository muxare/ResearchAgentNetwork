namespace ResearchAgentNetwork;

public class Citation
{
    public string Id { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty; // could be URL or memory ID
    public string Locator { get; set; } = string.Empty;  // page/section
    public string QuoteHash { get; set; } = string.Empty;
}

