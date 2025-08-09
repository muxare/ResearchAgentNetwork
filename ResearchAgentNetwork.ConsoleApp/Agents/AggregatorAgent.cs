using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class AggregatorAgent : IResearchAgent
{
    public string Role => "Aggregator";
    private readonly Func<Guid, List<ResearchTask>> _getChildren;

    public AggregatorAgent(Func<Guid, List<ResearchTask>> getChildren)
    {
        _getChildren = getChildren;
    }

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        var subTaskResults = await GetSubTaskResults(task);

        if (subTaskResults.All(r => r.Status == TaskStatus.Completed || r.Status == TaskStatus.Failed))
        {
            var aggregatedResult = await AggregateResults(subTaskResults, task, kernel);
            return new AgentResponse { Success = true, Data = aggregatedResult };
        }

        return new AgentResponse { Success = false, Message = "Not all subtasks completed" };
    }

    private Task<List<ResearchTask>> GetSubTaskResults(ResearchTask task)
    {
        var children = task.Id == Guid.Empty ? new List<ResearchTask>() : _getChildren(task.Id);
        return Task.FromResult(children);
    }

    private async Task<ResearchResult> AggregateResults(List<ResearchTask> subTasks, ResearchTask parentTask, Kernel kernel)
    {
        var completed = subTasks.Where(s => s.Status == TaskStatus.Completed && s.Result != null).ToList();
        var failed = subTasks.Where(s => s.Status == TaskStatus.Failed).ToList();

        string Summarize(ResearchTask t)
        {
            var content = t.Result?.Content ?? string.Empty;
            var max = 800;
            if (content.Length > max) content = content.Substring(0, max) + "...";
            return $"- {t.Description}:\n{content}";
        }

        var summaryBlock = string.Join("\n\n", completed.Select(Summarize));
        var failedBlock = failed.Any()
            ? ("The following subtasks failed and should be treated cautiously in synthesis: " + string.Join(", ", failed.Select(f => f.Description)))
            : "";

        var prompt = $@"You will synthesize multiple child research findings into a coherent report.
Parent task: {parentTask.Description}

Child summaries:
{summaryBlock}

{failedBlock}

Return a JSON object with fields: content (string, the final synthesized report) and sources (array of strings with URLs or citations, deduplicated).";

        var structured = await kernel.WithStructuredOutputRetry<ExecutorAgent.ExecutorStructuredResult>(prompt);

        var childSources = completed.SelectMany(t => t.Result?.Sources ?? new()).Distinct().ToList();
        var sources = (structured.Sources != null && structured.Sources.Any()) ? structured.Sources : childSources;

        return new ResearchResult
        {
            Content = structured.Content ?? string.Empty,
            ConfidenceScore = CalculateAggregateConfidence(completed),
            Sources = sources
        };
    }

    private double CalculateAggregateConfidence(List<ResearchTask> subTasks)
    {
        if (!subTasks.Any()) return 0;
        return subTasks.Average(t => t.Result?.ConfidenceScore ?? 0);
    }
}