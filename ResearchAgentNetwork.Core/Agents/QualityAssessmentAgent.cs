using Microsoft.SemanticKernel;
using System.Text.Json;

namespace ResearchAgentNetwork;

public class QualityAssessmentAgent : IResearchAgent
{
    public string Role => "QualityAssessor";

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        if (task.Result == null) return new AgentResponse { Success = false };

        var assessment = await AssessResultQuality(task.Result, task, kernel);
        task.Metadata["QualityAssessment"] = assessment;

        if (assessment.NeedsMoreResearch)
        {
            var additionalTasks = await GenerateFollowUpTasks(assessment, task, kernel);
            return new AgentResponse { Success = true, Data = additionalTasks };
        }

        return new AgentResponse { Success = true };
    }

    private async Task<List<ResearchTask>> GenerateFollowUpTasks(QualityAssessment assessment, ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Generate follow-up research tasks to address these gaps.
Original task: {task.Description}
Gaps identified: {string.Join(", ", assessment.Gaps)}

Return ONLY a valid JSON array of strings. No prose, no markdown, no HTML.";

        var descriptions = await kernel.WithStructuredOutputRetry<List<string>>(prompt);

        return descriptions.Select(d => new ResearchTask { Description = d }).ToList();
    }

    private async Task<QualityAssessment> AssessResultQuality(ResearchResult result, ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Assess the quality of this research result.
Task: {task.Description}
Result: {result.Content}

Return ONLY valid JSON with fields: needsMoreResearch (bool), reasoning (string), gaps (string array). No prose, no markdown, no HTML.";

        return await kernel.WithStructuredOutputRetry<QualityAssessment>(prompt);
    }
}