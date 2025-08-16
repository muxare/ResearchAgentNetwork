namespace ResearchAgentNetwork;

public class ReportSectionDraft
{
    public string SectionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ContentMd { get; set; } = string.Empty;
    public List<string> Citations { get; set; } = new();
    public List<string> FactsUsed { get; set; } = new();
    public double Confidence { get; set; }
}

