using Microsoft.SemanticKernel;
using ResearchAgentNetwork.SemanticMemory;
using ResearchAgentNetwork.WebSearch;

namespace ResearchAgentNetwork;

public class WebSearchAgent : IResearchAgent
{
    private readonly IWebSearchService _webSearchService;
    private readonly ISemanticMemoryService? _memory;

    public WebSearchAgent(IWebSearchService webSearchService, ISemanticMemoryService? memory)
    {
        _webSearchService = webSearchService;
        _memory = memory;
    }

    public string Role => "WebSearch";

    public async Task<AgentResponse> ProcessAsync(ResearchTask task, Kernel kernel)
    {
        try
        {
            var query = task.Description;
            if (string.IsNullOrWhiteSpace(query))
            {
                return new AgentResponse { Success = false, Message = "Empty query" };
            }

            var results = await _webSearchService.SearchAsync(query, topK: 5);
            if (results.Count == 0)
            {
                return new AgentResponse { Success = true, Message = "No results" };
            }

            // Collect short snippets for execution context
            var snippets = new List<string>();
            foreach (var r in results)
            {
                var text = (r.Snippet ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    snippets.Add($"{r.Title}\n{r.Url}\n{text}");
                }

                // Opportunistically index into vector memory if available
                if (_memory != null && !string.IsNullOrWhiteSpace(text))
                {
                    var rr = new ResearchResult
                    {
                        Content = text,
                        Sources = new List<string> { r.Url },
                        ConfidenceScore = 0.0,
                        RequiresAdditionalResearch = false
                    };
                    // Fire and forget to avoid blocking hot path
                    _ = _memory.IndexResultAsync(task, rr);
                }
            }

            if (snippets.Count > 0)
            {
                task.Metadata["RetrievedContext"] = snippets;
            }

            return new AgentResponse { Success = true, Message = $"Fetched {results.Count} results", Data = results };
        }
        catch (Exception ex)
        {
            return new AgentResponse { Success = false, Message = ex.Message };
        }
    }
}

