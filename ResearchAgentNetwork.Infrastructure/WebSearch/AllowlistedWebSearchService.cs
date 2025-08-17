using ResearchAgentNetwork.WebSearch;

namespace ResearchAgentNetwork.Infrastructure.WebSearch;

public class AllowlistedWebSearchService : IWebSearchService
{
    private readonly IWebSearchService _inner;
    private readonly HashSet<string> _allowedDomains;

    public AllowlistedWebSearchService(IWebSearchService inner, IEnumerable<string> allowedDomains)
    {
        _inner = inner;
        _allowedDomains = new HashSet<string>((allowedDomains ?? Array.Empty<string>())
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => !string.IsNullOrWhiteSpace(d)));
    }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var results = await _inner.SearchAsync(query, topK, cancellationToken).ConfigureAwait(false);
        if (_allowedDomains.Count == 0) return results;
        return results.Where(r => IsAllowed(r.Url)).ToList();
    }

    private bool IsAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        try
        {
            var u = new Uri(url, UriKind.Absolute);
            var host = u.Host.ToLowerInvariant();
            // Match full host or parent domain
            return _allowedDomains.Any(allowed => host == allowed || host.EndsWith("." + allowed));
        }
        catch
        {
            return false;
        }
    }
}

