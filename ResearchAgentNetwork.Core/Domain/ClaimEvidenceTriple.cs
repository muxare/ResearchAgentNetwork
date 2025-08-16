namespace ResearchAgentNetwork;

public class ClaimEvidenceTriple
{
    public string Claim { get; set; } = string.Empty;
    public string EvidenceText { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public double SupportScore { get; set; }
}

