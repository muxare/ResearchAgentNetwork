using ResearchAgentNetwork.WebSearch;

namespace ResearchAgentNetwork.Infrastructure.WebSearch;

public class NoOpWebSearchService : IWebSearchService
{
    public Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<WebSearchResult>>(Array.Empty<WebSearchResult>());
    }
}