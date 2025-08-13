using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class RetrievalDecisionAgent : IResearchAgent
{
    public string Role => "RetrievalDecision";

    public class RetrievalDecision
    {
        public bool RequireRetrieval { get; set; }
        public string Reason { get; set; } = string.Empty;
        public List<string> RetrievalTypes { get; set; } = new(); // e.g., ["vector", "web"]
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Decide whether retrieval is needed for the following research task.
Return ONLY valid JSON with fields: RequireRetrieval (boolean), Reason (string), RetrievalTypes (array of strings, allowed values: 'vector','web').

Task: {task.Description}

Consider novelty, need for up-to-date facts, and breadth of knowledge.";

        var decision = await kernel.WithStructuredOutputRetry<RetrievalDecision>(prompt);

        // Guardrails: normalize
        decision.RetrievalTypes = decision.RetrievalTypes
            ?.Where(t => string.Equals(t, "vector", StringComparison.OrdinalIgnoreCase) || string.Equals(t, "web", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList() ?? new List<string>();

        return new AgentResponse { Success = true, Data = decision };
    }
}

