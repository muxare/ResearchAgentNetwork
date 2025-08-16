using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class ReportOutlineAgent : IResearchAgent
{
    public string Role => "ReportOutline";

    public class OutlinePlan
    {
        public List<OutlineSection>? Sections { get; set; }
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Create a structured report outline for the following research task.
Return ONLY valid JSON with fields Sections (array). Each section object must have Id (string), Title (string), Purpose (string), AcceptanceCriteria (array of strings).

Task: {task.Description}";

        var plan = await kernel.WithStructuredOutputRetry<OutlinePlan>(prompt);

        var outline = new ReportOutline
        {
            RootTaskId = task.Id,
            Sections = plan.Sections ?? new List<OutlineSection>()
        };

        task.Metadata["ReportOutline"] = outline;
        return new AgentResponse { Success = true, Data = outline };
    }
}

