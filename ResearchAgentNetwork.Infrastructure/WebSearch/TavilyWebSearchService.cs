using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ResearchAgentNetwork.WebSearch;

namespace ResearchAgentNetwork.Infrastructure.WebSearch;

public class TavilyWebSearchService : IWebSearchService
{
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly HttpClient _httpClient;

    public TavilyWebSearchService(string apiKey, string? baseUrl = null, HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("Tavily API key is required", nameof(apiKey));
        _apiKey = apiKey;
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.tavily.com" : baseUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<WebSearchResult>();
        var url = _baseUrl + "/search";

        // Minimal request payload for Tavily API
        var payload = new Dictionary<string, object?>
        {
            ["api_key"] = _apiKey,
            ["query"] = query,
            ["max_results"] = Math.Clamp(topK, 1, 10),
            ["search_depth"] = "basic",
            ["include_answer"] = false,
            ["include_images"] = false,
            ["include_domains"] = _includeDomains?.Length > 0 ? _includeDomains : null
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _httpClient.PostAsync(url, content, cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            return Array.Empty<WebSearchResult>();
        }

        using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);
        var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var results = new List<WebSearchResult>();
        if (doc.RootElement.TryGetProperty("results", out var resultsEl) && resultsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultsEl.EnumerateArray())
            {
                var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                var urlVal = item.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                var snippet = item.TryGetProperty("content", out var s) ? s.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(urlVal) && string.IsNullOrWhiteSpace(snippet))
                {
                    continue;
                }
                results.Add(new WebSearchResult(title, urlVal, snippet));
            }
        }

        return results;
    }

    private string[]? _includeDomains;

    public TavilyWebSearchService WithIncludeDomains(IEnumerable<string> domains)
    {
        _includeDomains = domains?.Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(d => d.Trim().ToLowerInvariant())
            .Distinct()
            .ToArray();
        return this;
    }
}

