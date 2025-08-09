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
        var prompt = $@"Generate follow-up research tasks to address these gaps:
                Original task: {task.Description}
                Gaps identified: {string.Join(", ", assessment.Gaps)}
                
                Return JSON array of task descriptions.";

        var result = await kernel.InvokePromptAsync(prompt);
        var jsonResult = result.GetValue<string>() ?? "[]";
        var descriptions = JsonSerializer.Deserialize<List<string>>(jsonResult) ?? new List<string>();

        return descriptions.Select(d => new ResearchTask { Description = d }).ToList();
    }

    private async Task<QualityAssessment> AssessResultQuality(ResearchResult result, ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Assess the quality of this research result:
                Task: {task.Description}
                Result: {result.Content}
                
                Return JSON: {{needsMoreResearch: bool, reasoning: string, gaps: string[]}}";

        var result2 = await kernel.InvokePromptAsync(prompt);
        var jsonResult = result2.GetValue<string>() ?? "{}";
        return JsonSerializer.Deserialize<QualityAssessment>(jsonResult) ?? new QualityAssessment();
    }
}