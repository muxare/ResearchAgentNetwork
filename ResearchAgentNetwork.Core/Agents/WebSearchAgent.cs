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
            // Use planned queries if present; otherwise default to task description
            var queries = new List<string>();
            if (task.Metadata.TryGetValue("PlannedQueries", out var pq) && pq is List<string> planned && planned.Count > 0)
            {
                queries = planned;
            }
            else if (!string.IsNullOrWhiteSpace(task.Description))
            {
                queries = new List<string> { task.Description };
            }
            if (queries.Count == 0)
            {
                return new AgentResponse { Success = false, Message = "Empty query" };
            }

            // Collect items with provenance for execution context
            var items = new List<RetrievedItem>();
            int totalFetched = 0;
            foreach (var query in queries)
            {
                var results = await _webSearchService.SearchAsync(query, topK: 5);
                totalFetched += results.Count;
                if (results.Count == 0) continue;

                foreach (var r in results)
                {
                    var text = (r.Snippet ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        items.Add(new RetrievedItem(
                            Kind: "web",
                            Snippet: text,
                            Title: r.Title,
                            Url: r.Url,
                            Score: null,
                            ChunkIndex: null,
                            TotalChunks: null
                        ));
                    }

                    // Opportunistically index into vector memory if available
                    if (_memory != null && !string.IsNullOrWhiteSpace(text))
                    {
                        var rr = new ResearchResult
                        {
                            Content = text,
                            Sources = new List<string> { r.Url },
                            ConfidenceScore = 0.0,
                            RequiresAdditionalResearch = false,
                            Metadata = new Dictionary<string, object>
                            {
                                ["sourceTitle"] = r.Title ?? string.Empty,
                                ["sourceUrl"] = r.Url ?? string.Empty,
                                ["ingestedFrom"] = "websearch"
                            }
                        };
                        // Fire and forget to avoid blocking hot path
                        _ = _memory.IndexResultAsync(task, rr);
                    }
                }
            }

            if (items.Count > 0)
            {
                task.Metadata["RetrievedContext"] = items;
            }

            return new AgentResponse { Success = true, Message = $"Fetched {totalFetched} results", Data = null };
        }
        catch (Exception ex)
        {
            return new AgentResponse { Success = false, Message = ex.Message };
        }
    }
}

