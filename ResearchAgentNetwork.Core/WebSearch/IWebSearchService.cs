namespace ResearchAgentNetwork.WebSearch;

public interface IWebSearchService
{
    Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
}

public record WebSearchResult(string Title, string Url, string Snippet);