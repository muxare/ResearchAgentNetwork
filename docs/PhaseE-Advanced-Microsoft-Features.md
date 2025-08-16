<!-- markdownlint-disable MD041 -->
## Phase E: Advanced Microsoft Features

### Overview
Introduces knowledge curation and memory routing hooks to improve retrieval quality and organization, leveraging our SK-based memory and LLM planning.

### What Changed
- Domain: `MemoryMergeSuggestion`
- Agents: `KnowledgeCuratorAgent`, `MemoryRouterAgent`
- Orchestrator: after result storage, runs routing decision and curation; emits `routed` and `curated`

### Data Flow
1) After storing a result in semantic memory
2) `MemoryRouterAgent` decides collection/tags/retention (metadata only, no destructive ops)
3) `KnowledgeCuratorAgent` suggests merge groups for potential deduplication
4) Results saved in `MemoryRouteApplied` and `CurateOutput` metadata entries

### Execution Flow
- These hooks run opportunistically post-store and do not block the main flow

### Configuration
No new settings required for Phase E.

### How to Test
1) Submit a task; ensure result is stored (meets policy)
2) Watch `/api/events` for `routed` and `curated`
3) Inspect `/api/tasks/{id}` metadata for `MemoryRouteApplied` and `CurateOutput`

### Notes
- Suggestions are advisory; future work can implement automatic merges via vector store APIs
- Routing data is currently advisory and can inform collection naming/tagging in future adapters

