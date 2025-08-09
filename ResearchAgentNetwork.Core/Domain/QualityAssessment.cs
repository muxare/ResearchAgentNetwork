namespace ResearchAgentNetwork;

public class QualityAssessment
{
    public bool NeedsMoreResearch { get; set; }
    public string Reasoning { get; set; } = "";
    public List<string> Gaps { get; set; } = new();
}