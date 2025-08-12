## Simple Research Assistant — Phase 6: Web Search + Ingestion (Feature-flagged)

Scope

- Add `WebSearchAgent` that uses `IWebSearchService` to fetch fresh sources, emit ingestion summary, and enrich execution context.
- Feature flag: `ResearchAgent:EnableWebSearch` (default false). When disabled, system behavior is unchanged.
- Qdrant is recommended for memory (`VectorDb:Provider=Qdrant`).

Architecture / Data Flow

1) Orchestrator at Executing stage:
   - Retrieves similar results from vector memory (existing behavior)
   - If `EnableWebSearch=true`, calls `websearch` agent → appends snippets to `task.Metadata["RetrievedContext"]`
2) Executor uses `RetrievedContext` to augment prompt context and produce better sources/content.
3) Agent opportunistically indexes fetched snippets into vector memory for future reuse.

Configuration

- `ResearchAgent:EnableWebSearch`: `true|false`
- `WebSearch:Provider`: `None` (placeholder)
- Vector memory:
  - `VectorDb:Provider=Qdrant`
  - `VectorDb:Endpoint=http://localhost:6334`

Testing

1) Console
```bash
setx ResearchAgent__EnableWebSearch true
setx VectorDb__Provider Qdrant
dotnet run --project ResearchAgentNetwork.ConsoleApp
```
Observe ingestion events and improved context.

2) Web
```bash
setx ResearchAgent__EnableWebSearch true
setx VectorDb__Provider Qdrant
dotnet run --project ResearchAgentNetwork.Web
```
Open UI → submit a task → watch SSE events (`ingested`), then open the report.

Next

- Replace `NoOpWebSearchService` with a real provider (Bing/Google/Tavily wrapper) and add allowlist/rate limiting.

