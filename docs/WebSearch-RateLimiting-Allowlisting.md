## Web Search — Rate Limiting and Allowlisting

### What was implemented

- Added `RateLimitedWebSearchService` wrapper to enforce:
  - Requests-per-minute window (token-less, timestamp queue)
  - Minimum interval between requests (ms)
- Added `AllowlistedWebSearchService` wrapper to filter results to a configured set of domains.
- Extended `TavilyWebSearchService` with `WithIncludeDomains()` to request server-side domain filtering when possible.
- Wired configuration in both Web and Console hosts:
  - `WebSearch:Allowlist` (CSV of domains, e.g., `nih.gov,who.int`)
  - `WebSearch:RateLimit:RPM` (default 30)
  - `WebSearch:RateLimit:MinIntervalMs` (default 500)

### How to use

- Set in appsettings or environment variables, e.g.:
```
WebSearch__Allowlist=nih.gov,who.int
WebSearch__RateLimit__RPM=20
WebSearch__RateLimit__MinIntervalMs=750
```

### Notes

- Allowlisting filters client-side for all providers; when using Tavily API it also applies server-side via `include_domains`.
- Rate limiting is cooperative on the client; ensure provider account limits are respected.

