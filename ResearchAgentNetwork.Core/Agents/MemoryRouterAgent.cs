using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class MemoryRouterAgent : IResearchAgent
{
    public string Role => "MemoryRouter";

    public class RouteDecision
    {
        public string Collection { get; set; } = "results";
        public List<string> Tags { get; set; } = new();
        public string Retention { get; set; } = "standard"; // standard|long|short
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        var content = task.Result?.Content ?? string.Empty;
        var prompt = $@"Decide routing for semantic memory storage. Return ONLY valid JSON with fields Collection (string), Tags (array of strings), Retention (string: standard|long|short).

Content:
{content}";
        var decision = await kernel.WithStructuredOutputRetry<RouteDecision>(prompt);
        task.Metadata["MemoryRoute"] = decision;
        return new AgentResponse { Success = true, Data = decision };
    }
}

