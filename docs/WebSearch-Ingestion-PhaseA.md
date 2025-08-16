<!-- markdownlint-disable MD041 -->
## Phase A: Web Search + Ingestion MVP (Microsoft-first)

### Overview
Implements the initial web search ingestion slice using Microsoft Semantic Kernel where possible. Web results are ingested as chunked embeddings with provenance metadata and made available for downstream retrieval and task execution.

### Scope
- Web search: Tavily via SK plugin or direct API
- Semantic memory: embedding + vector store (in-memory or Qdrant)
- Provenance: url/title preserved where available; chunk indices recorded

### Architecture Impact
- Updated `WebSearchAgent` to attach provenance metadata and opportunistically index results into semantic memory
- Updated `SemanticMemoryService.IndexResultAsync` to:
  - generate unique ids per chunk (prevents overwrites)
  - store provenance fields in `Metadata` (e.g., `url`, `chunkIndex`, `totalChunks`)
- Updated `SkVectorStoreAdapter` to persist/retrieve `Url`, `ChunkIndex`, `TotalChunks` alongside payload
- Web wiring (`ResearchAgentNetwork.Web/Program.cs`) to allow:
  - `WebSearch:Provider = Tavily` (SK plugin; snippets only)
  - `WebSearch:Provider = TavilyApi` (direct API; includes title/url)

### Decisions (confirmed)
- **Provider standardization**: `TavilyApi` (direct API) for richer provenance (title/url). We keep `Tavily` (SK plugin) as a fallback.
- **Default behavior**: **Web search enabled by default** in development (`ResearchAgent:EnableWebSearch = true`).
- **Runtime controls**: We will add runtime toggles for merge/reuse thresholds and the web search flag to enable quick iteration.

### Phased Plan
- **Phase 1: Ship WebSearch MVP end-to-end**
  - Ensure `ResearchAgent:EnableWebSearch = true` and `WebSearch:Provider = TavilyApi` with API key configured.
  - Manual test: submit a task; verify Activity shows `ingested`, and that execution includes retrieved context.
- **Phase 2: Runtime toggle for WebSearch (planned)**
  - Orchestrator adds `UpdateWebSearchEnabled(bool)`; `/api/settings` returns/accepts `enableWebSearch`.
  - `SettingsPanel` adds a checkbox to toggle this at runtime (no restart).
- **Phase 3: Activity hydration on load (planned)**
  - `ActivityPanel` backfills timeline on mount/selection via `/api/tasks/{id}/events?includeChildren=true&top=300`, then continues via SSE.
- **Phase 4: Query-planned WebSearch (planned)**
  - Reuse `QueryPlanner` queries for web search; iterate over `task.Metadata["PlannedQueries"]` to fetch, index, and attach context.
- **Phase 5: Tests (planned)**
  - Unit tests for `WebSearchAgent` with mocked `IWebSearchService` and `ISemanticMemoryService`.
  - Orchestrator integration tests to verify `ingested`/`retrieved` events and on/off behavior.
- **Phase 6: Threshold toggles (planned)**
  - Add runtime settings for `PendingThreshold`, `CompletedThreshold`, `StoreMinConfidence`, and `DuplicateThreshold` to accelerate tuning.

### Data Flow
1) `ResearchOrchestrator` decides to enrich with web search (feature-gated)
2) `WebSearchAgent` executes provider query → collects snippets
3) Each snippet is chunked and embedded via `SemanticMemoryService`
4) Chunks are stored in vector store with provenance
5) Retrieval paths can include these chunks to enrich execution context

### Execution Flow (per task)
- Submit task → optional memory retrieval → optional web search ingestion → execute → assess → (store result when above thresholds)

### Configuration
`ResearchAgentNetwork.Web/appsettings.json`
```json
{
  "ResearchAgent": { "EnableWebSearch": true },
  "VectorDb": { "Provider": "Qdrant", "Endpoint": "localhost:6334", "CollectionPrefix": "ran", "TopK": 5 },
  "WebSearch": {
    "Provider": "TavilyApi", // standardized provider; use "Tavily" for SK plugin fallback
    "Tavily": { "ApiKey": "<your-key>" }
  }
}
```

Environment variables equivalent (examples):
```
ResearchAgent__EnableWebSearch=true
VectorDb__Provider=Qdrant
VectorDb__Endpoint=localhost:6334
WebSearch__Provider=TavilyApi
WebSearch__Tavily__ApiKey=tvly-...  
```

### How to Test
1) Build and run web host
```
dotnet build ./ResearchAgentNetwork.sln -c Debug
dotnet run --project ResearchAgentNetwork.Web
```
2) Submit a task (replace the description as needed)
```
curl -s -X POST http://localhost:5000/api/tasks -H "Content-Type: application/json" \
  -d '{"Description":"Latest research on GLP-1 receptor agonists side effects","Priority":5}'
```
3) Observe events (ingestion and completion)
```
curl -N http://localhost:5000/api/events
```
4) Retrieve task report
```
curl http://localhost:5000/api/tasks/<TASK_ID>/report
```

Expected:
- Event with `EventType = ingested` after web search completes
- Event with `EventType = retrieved` when memory retrieval occurs
- Final `completed` event and report text

### UI Impact
- **Settings**
  - Adds (planned) runtime toggle for Web Search and thresholds in `SettingsPanel` via `/api/settings`.
- **Activity**
  - Displays `ingested`, `retrieved`, `stored`, `routed`, `curated`, etc. with agent hints.
  - (Planned) Backfill on page load/selection using `/api/tasks/{id}/events` to complement SSE.

### API Endpoints used
- `POST /api/tasks` — submit task
- `GET /api/events` — SSE stream for live activity
- `GET /api/tasks/{id}/events?includeChildren=true&top=300` — timeline backfill (planned)
- `GET /api/tasks/{id}/report` — current report
- `GET /api/tasks/{id}/report/persisted` — last persisted report
- `POST /api/settings` — update runtime settings (to include `enableWebSearch`, planned)
- `GET /api/settings` — fetch runtime settings

### Notes & Limitations
- SK Tavily plugin (`Provider = Tavily`) returns snippets only (no url/title). Use `TavilyApi` for richer provenance.
- Chunking uses 1500 chars (150 overlap). Tune later as needed.
- Qdrant collections are typed with 768-dim embeddings by default.

### Verification Checklist
- [ ] Set `WebSearch__Provider=TavilyApi` and `WebSearch__Tavily__ApiKey=...` in environment.
- [ ] Start the web host; submit a task; observe `ingested` in Activity.
- [ ] Confirm `RetrievedContext` improves executor outputs and that final report quality is higher.
- [ ] Toggle web search off (once Phase 2 is implemented) and verify `ingested` disappears while other flow remains.

### Next (Phase B preview)
- Introduce outline planning with Microsoft plugins for text/memory
- Add deterministic query planning for follow-up retrieval

