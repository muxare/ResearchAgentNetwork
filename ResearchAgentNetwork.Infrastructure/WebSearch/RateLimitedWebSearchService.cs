using ResearchAgentNetwork.WebSearch;

namespace ResearchAgentNetwork.Infrastructure.WebSearch;

public class RateLimitedWebSearchService : IWebSearchService
{
    private readonly IWebSearchService _inner;
    private readonly int _requestsPerMinute;
    private readonly int _minIntervalMs;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly Queue<DateTime> _requestTimestamps = new();
    private DateTime _lastRequestUtc = DateTime.MinValue;

    public RateLimitedWebSearchService(IWebSearchService inner, int requestsPerMinute, int minIntervalMs)
    {
        _inner = inner;
        _requestsPerMinute = Math.Max(1, requestsPerMinute);
        _minIntervalMs = Math.Max(0, minIntervalMs);
    }

    public async Task<IReadOnlyList<WebSearchResult>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        await ThrottleAsync(cancellationToken).ConfigureAwait(false);
        return await _inner.SearchAsync(query, topK, cancellationToken).ConfigureAwait(false);
    }

    private async Task ThrottleAsync(CancellationToken ct)
    {
        await _mutex.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            // Enforce min interval between requests
            if (_minIntervalMs > 0 && _lastRequestUtc != DateTime.MinValue)
            {
                var elapsedMs = (int)(now - _lastRequestUtc).TotalMilliseconds;
                if (elapsedMs < _minIntervalMs)
                {
                    var delayMs = _minIntervalMs - elapsedMs;
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, ct).ConfigureAwait(false);
                        now = DateTime.UtcNow;
                    }
                }
            }

            // Enforce requests per minute window
            var windowStart = now.AddMinutes(-1);
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
            {
                _requestTimestamps.Dequeue();
            }
            if (_requestTimestamps.Count >= _requestsPerMinute)
            {
                var earliest = _requestTimestamps.Peek();
                var wait = earliest.AddMinutes(1) - now;
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait, ct).ConfigureAwait(false);
                }
                now = DateTime.UtcNow;
                // Clean again after wait
                windowStart = now.AddMinutes(-1);
                while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
                {
                    _requestTimestamps.Dequeue();
                }
            }

            _requestTimestamps.Enqueue(DateTime.UtcNow);
            _lastRequestUtc = DateTime.UtcNow;
        }
        finally
        {
            _mutex.Release();
        }
    }
}

