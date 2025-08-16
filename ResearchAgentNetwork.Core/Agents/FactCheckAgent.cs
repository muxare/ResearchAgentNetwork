using Microsoft.SemanticKernel;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork;

public class FactCheckAgent : IResearchAgent
{
    private readonly ISemanticMemoryService? _memory;
    public FactCheckAgent(ISemanticMemoryService? memory) { _memory = memory; }

    public string Role => "FactCheck";

    public class FactCheckInput
    {
        public string SectionId { get; set; } = string.Empty;
        public string ContentMd { get; set; } = string.Empty;
    }

    public class FactCheckOutput
    {
        public List<ClaimEvidenceTriple> Items { get; set; } = new();
        public double VerifiedRatio { get; set; }
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        if (!task.Metadata.TryGetValue("FactCheckInput", out var obj) || obj is not FactCheckInput input)
        {
            return new AgentResponse { Success = false, Message = "Missing FactCheckInput" };
        }

        var prompt = $@"Extract factual claims from the section and match each to supporting evidence using web search or memory.
Return ONLY valid JSON with fields: Items (array of objects with Claim, EvidenceText, SourceUrl, SupportScore), VerifiedRatio (0..1).

Section Markdown:
{input.ContentMd}";

        var output = await kernel.WithStructuredOutputRetry<FactCheckOutput>(prompt);
        task.Metadata[$"FactCheck:{input.SectionId}"] = output;
        return new AgentResponse { Success = true, Data = output };
    }
}

