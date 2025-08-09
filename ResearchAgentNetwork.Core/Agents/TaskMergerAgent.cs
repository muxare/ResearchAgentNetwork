using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class TaskMergerAgent : IResearchAgent
{
    public string Role => "TaskMerger";

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        var similarTasks = await FindSimilarTasks(task);

        if (similarTasks.Count > 0)
        {
            var mergedTask = await MergeTasks(task, similarTasks, kernel);
            return new AgentResponse { Success = true, Data = mergedTask };
        }

        return new AgentResponse { Success = true };
    }

    private Task<List<ResearchTask>> FindSimilarTasks(ResearchTask task)
    {
        return Task.FromResult(new List<ResearchTask>());
    }

    private async Task<ResearchTask> MergeTasks(ResearchTask task, List<ResearchTask> similarTasks, Kernel kernel)
    {
        var schema = JsonStuff.GenerateJsonSchemaFromClass<ResearchTask>();
        var prompt = $@"Merge these similar research tasks:
                Main task: {task.Description}
                Similar tasks: {string.Join(", ", similarTasks.Select(t => t.Description))}

                Create a unified task description that covers all aspects and return as JSON: {schema}.";

        var result = await kernel.InvokePromptAsync(prompt);
        var mergedDescription = result.GetValue<string>() ?? task.Description;

        return new ResearchTask { Description = mergedDescription };
    }
}