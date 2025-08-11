using Microsoft.SemanticKernel;

namespace ResearchAgentNetwork;

public class ExecutorAgent : IResearchAgent
{
    public string Role => "Executor";

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        if (task.Metadata.TryGetValue("ForceExecute", out var forceObj) && forceObj is bool force && force)
        {
            var forced = await ExecuteWithLLM(task, kernel);
            return new AgentResponse { Success = true, Data = forced };
        }

        if (await IsAtomicTask(task, kernel))
        {
            var result = await ExecuteWithLLM(task, kernel);
            return new AgentResponse { Success = true, Data = result };
        }

        return new AgentResponse { Success = false, Message = "Task not atomic enough for execution" };
    }

    private async Task<ResearchResult> ExecuteWithLLM(ResearchTask task, Kernel kernel)
    {
        var context = BuildContext(task);
        var prompt = $@"Research the following topic thoroughly and produce a structured result.
Topic: {task.Description}

Context: {context}

Return ONLY valid JSON with fields content (string) and sources (array of strings). No prose, no markdown, no HTML. Escape newlines in content as \\n.";

        var structured = await kernel.WithStructuredOutputRetry<ExecutorStructuredResult>(prompt);

        var content = structured.Content ?? string.Empty;
        var sources = structured.Sources?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();

        var sanitized = SanitizeContent(content);
        var quality = await EvaluateQuality(sanitized, kernel);
        var completeness = await CheckCompleteness(sanitized, task, kernel);

        task.Metadata["QualityScore"] = quality;
        task.Metadata["Completeness"] = new Completeness { NeedsMoreResearch = completeness, Reasoning = string.Empty };

        return new ResearchResult
        {
            Content = content,
            Sources = sources,
            ConfidenceScore = quality,
            RequiresAdditionalResearch = completeness
        };
    }

    private string BuildContext(ResearchTask task)
    {
        var baseContext = $"Research context for: {task.Description}";
        if (task.Metadata.TryGetValue("RetrievedContext", out var ctxObj) && ctxObj is List<string> snippets && snippets.Count > 0)
        {
            // Limit to top 3 snippets and cap total length to keep prompts bounded
            var top = snippets.Where(s => !string.IsNullOrWhiteSpace(s)).Take(3).ToList();
            var joined = string.Join("\n---\n", top);
            if (joined.Length > 2000)
            {
                joined = joined.Substring(0, 2000);
            }
            return baseContext + "\n\nRetrieved Context:\n" + joined;
        }
        return baseContext;
    }

    private async Task<double> EvaluateQuality(string content, Kernel kernel)
    {
        var prompt = $@"Rate the quality of this research content from 0.0 to 1.0.
Content:
{content}

Consider comprehensiveness, accuracy, and relevance.";

        var quality = await kernel.WithStructuredOutputRetry<QualityScore>(prompt);
        return quality.Score;
    }

    public class Completeness
    {
        public bool NeedsMoreResearch { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public class AtomicityCheck
    {
        public bool IsAtomic { get; set; }
        public string Reasoning { get; set; } = string.Empty;
    }

    public class QualityScore
    {
        public double Score { get; set; }
    }

    public class ExecutorStructuredResult
    {
        public string Content { get; set; } = string.Empty;
        public List<string>? Sources { get; set; }
    }

    private async Task<bool> CheckCompleteness(string content, ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Assess whether this research content requires additional investigation.
Task: {task.Description}
Content:
{content}";

        var completeness = await kernel.WithStructuredOutputRetry<Completeness>(prompt);
        return completeness.NeedsMoreResearch;
    }

    private static string SanitizeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        var text = content.Trim();
        if (text.StartsWith("```"))
        {
            var idx = text.IndexOf('\n');
            if (idx > 0) text = text.Substring(idx + 1);
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (end > 0) text = text.Substring(0, end);
        }
        // Remove basic HTML tags
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", string.Empty);
        // Normalize newlines
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return text;
    }

    private async Task<bool> IsAtomicTask(ResearchTask task, Kernel kernel)
    {
        var prompt = $@"Is this research task atomic enough for direct execution?
Task: {task.Description}

Consider if it's focused, specific, and can be answered directly.";

        var atomicity = await kernel.WithStructuredOutputRetry<AtomicityCheck>(prompt);
        return atomicity.IsAtomic;
    }
}