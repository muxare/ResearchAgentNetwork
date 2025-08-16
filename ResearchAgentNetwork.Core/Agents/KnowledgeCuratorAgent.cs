using Microsoft.SemanticKernel;
using ResearchAgentNetwork.SemanticMemory;

namespace ResearchAgentNetwork;

public class KnowledgeCuratorAgent : IResearchAgent
{
    private readonly ISemanticMemoryService? _memory;
    public KnowledgeCuratorAgent(ISemanticMemoryService? memory) { _memory = memory; }

    public string Role => "KnowledgeCurator";

    public class CurateInput
    {
        public string Query { get; set; } = string.Empty;
        public int TopK { get; set; } = 10;
        public double DuplicateThreshold { get; set; } = 0.98;
    }

    public class CurateOutput
    {
        public List<MemoryMergeSuggestion> Suggestions { get; set; } = new();
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        if (_memory is null)
        {
            return new AgentResponse { Success = false, Message = "No memory configured" };
        }
        if (!task.Metadata.TryGetValue("CurateInput", out var obj) || obj is not CurateInput input)
        {
            return new AgentResponse { Success = false, Message = "Missing CurateInput" };
        }

        // Retrieve top-K for a simple query and cluster identical/near duplicates via LLM (placeholder)
        var results = await _memory.RetrieveSimilarResultsAsync(input.Query, topK: input.TopK);
        var payload = string.Join("\n---\n", results.Select((r, i) => $"[{i}] score={r.Score:F3}\n{r.Payload}"));
        var prompt = $@"Identify near-duplicate items and propose merge groups.
Return ONLY valid JSON with Suggestions: array of objects with CanonicalId (guid) and DuplicateIds (array of guid).

Items:
{payload}";

        var output = await kernel.WithStructuredOutputRetry<CurateOutput>(prompt);
        task.Metadata["CurateOutput"] = output;
        return new AgentResponse { Success = true, Data = output };
    }
}

