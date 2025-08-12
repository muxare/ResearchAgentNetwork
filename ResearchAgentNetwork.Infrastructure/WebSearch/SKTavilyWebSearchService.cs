using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Plugins.Web.Tavily;
using ResearchAgentNetwork.WebSearch;

namespace ResearchAgentNetwork.Infrastructure.WebSearch;

public class SKTavilyWebSearchService : IWebSearchService
{
    private readonly TavilyTextSearch _tavily;

    public SKTavilyWebSearchService(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Tavily API key is required", nameof(apiKey));
        }
        _tavily = new TavilyTextSearch(apiKey);
    }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<WebSearchResult>();

        var options = new TextSearchOptions { Top = Math.Clamp(topK, 1, 10) };
        var searchResults = await _tavily.SearchAsync(query, options, cancellationToken).ConfigureAwait(false);

        var list = new List<WebSearchResult>();
        if (searchResults is null)
        {
            return list;
        }

        await foreach (var snippet in searchResults.Results.WithCancellation(cancellationToken))
        {
            var text = snippet ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                list.Add(new WebSearchResult(string.Empty, string.Empty, text));
                if (list.Count >= topK) break;
            }
        }

        return list;
    }
}

