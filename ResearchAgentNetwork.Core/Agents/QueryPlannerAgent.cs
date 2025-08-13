using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class QueryPlannerAgent : IResearchAgent
{
    public string Role => "QueryPlanner";

    public class QueryPlan
    {
        public List<string> Queries { get; set; } = new();
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Generate 1-3 focused retrieval queries for this task.
Return ONLY valid JSON with field Queries (array of strings).

Task: {task.Description}

Be concise; include key entities/constraints.";

        var plan = await kernel.WithStructuredOutputRetry<QueryPlan>(prompt);
        // Normalize and prune
        var queries = plan.Queries
            ?.Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q.Trim())
            .Distinct()
            .Take(3)
            .ToList() ?? new List<string>();

        return new AgentResponse { Success = true, Data = new QueryPlan { Queries = queries } };
    }
}

