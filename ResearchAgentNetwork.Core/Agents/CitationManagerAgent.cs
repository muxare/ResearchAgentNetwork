using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class CitationManagerAgent : IResearchAgent
{
    public string Role => "CitationManager";

    public class CitationNormalizeInput
    {
        public string SectionId { get; set; } = string.Empty;
        public string ContentMd { get; set; } = string.Empty;
        public List<string> RawCitations { get; set; } = new();
        public string Style { get; set; } = "APA";
    }

    public class CitationNormalizeOutput
    {
        public List<Citation> Citations { get; set; } = new();
        public string BibliographyMd { get; set; } = string.Empty;
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        if (!task.Metadata.TryGetValue("CitationNormalizeInput", out var obj) || obj is not CitationNormalizeInput input)
        {
            return new AgentResponse { Success = false, Message = "Missing CitationNormalizeInput" };
        }

        var prompt = $@"Normalize citations for the section content and produce a bibliography in {input.Style} style.
Return ONLY valid JSON with fields: Citations (array of {{Id, SourceId, Locator, QuoteHash}}), BibliographyMd (string).";

        var result = await kernel.WithStructuredOutputRetry<CitationNormalizeOutput>(prompt);
        task.Metadata[$"Citations:{input.SectionId}"] = result;
        return new AgentResponse { Success = true, Data = result };
    }
}

