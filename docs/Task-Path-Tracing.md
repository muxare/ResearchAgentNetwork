## Task Path Tracing — Plan

### Objectives

- End-to-end visibility of a task’s journey through all agents, including decisions.
- Persisted, queryable events with parent/child relationships.
- Realtime UI timeline with path-aware filtering.

### Current State

- Events: `TaskEvent` published by orchestrator, persisted in Sqlite and streamed via SSE `/api/events`.
- Event fields: `TaskId`, `ParentTaskId?`, `Status`, `EventType`, `TimestampUtc`, `Message?`.
- UI: `ActivityPanel` shows a flat event feed.

### Gaps

- Decision details are not standardized or structured; `Message` is free text.
- No subtree/path filtering in UI; no per-task timeline view.
- No API to fetch a task’s full history (with descendants) in chronological order.

### Design Overview

- Event model: keep existing fields; encode decision details in `Message` as compact JSON (`json:{...}`) for Phase 1. Optionally add `AgentRole` and `DetailsJson` columns in Phase 2.
- Publishing: emit start/decision/end events around agent calls with consistent event types and detail schemas.
- API: add endpoint to fetch task timeline with optional descendant aggregation.
- UI: add per-task Timeline, subtree filter in Activity, and expandable decision details.

### Standard Event Types

- Lifecycle: `submitted`, `status`, `decomposed`, `aggregated`, `retrieved`, `ingested`, `completed`, `failed`, `merged`, `stored`, `routed`, `curated`, `outlined`, `section_drafted`, `section_fact_checked`, `section_citations_normalized`.
- Agent scaffolding: `agent_started` and `agent_decision` with details.
- Agent decision detail schemas (examples):
  - `retrieval_decision`: `{ requireRetrieval: boolean, retrievalTypes: string[] }`
  - `query_plan`: `{ queries: string[] }`
  - `executor_context`: `{ model?: string, tokens?: { prompt?: number, completion?: number }, retrievedCount?: number }`
  - `memory_route`: `{ route: string, reason?: string }`
  - `curation`: `{ duplicatesFiltered: number, kept: number, threshold: number }`
  - `analyzer`: `{ subtaskCount: number }`
  - `websearch`: `{ provider: string, items: number }`
  - `section_writer`: `{ sectionId: string, title?: string }`
  - `fact_check`: `{ sectionId: string, outcome: string }`
  - `citation_manager`: `{ sectionId: string, style: string, normalized: number }`

### API Additions

- `GET /api/tasks/{id}/events?includeChildren={bool}&top={int}&skip={int}`
  - Returns events ordered by time; if `includeChildren=true`, include descendants (via `ParentTaskId`).

### UI Enhancements

- Task Timeline in `TaskDetails`: chronological events with badges for `agent` and `eventType`; expandable JSON details.
- Activity subtree filter: when a task is selected, show only its subtree (toggleable).
- Optional breadcrumb/tree navigation for parent/child traversal.

### Data & Execution Flow

- Submit → `submitted` → analyze (`agent_started`/`agent_decision`) → `decomposed` or continue
- Execute path: `status:Executing` → retrieval/plan decisions → `retrieved`/`ingested` → executor decisions → `completed` → `stored` → `routed` → `curated`
- Optional authoring path: `outlined` → `section_drafted` → `section_fact_checked` → `section_citations_normalized`

### Phased Implementation

- Phase 1 (no DB migration):
  - Emit missing `agent_started`/`agent_decision` events with `Message` as `json:{...}`.
  - Add `GET /api/tasks/{id}/events?includeChildren`.
  - UI: add Timeline component; subtree filter in Activity; render JSON details.

- Phase 2 (structured persistence + SSE):
  - Add `AgentRole` and `DetailsJson` to `TaskEventEntity`; include in SSE payload.
  - UI: badges for agent and pretty JSON rendering; fallback to `Message`.

- Phase 3 (visualization):
  - Breadcrumb/tree, filtering by agent/event type/time window; export timeline.
  - Status: Implemented filters (agent/type/since) in `ActivityPanel`, subtree toggle; report link and structured details in `TaskDetails` Timeline.

### Testing

- Orchestrator unit tests: assert event sequences with and without decomposition; validate decision payloads.
- API tests: verify ordering, inclusion of descendants, pagination.
- UI manual checks: live updates, subtree filter correctness, details rendering, decomposed task navigation.

### How to Test End-to-End

1. Start the web app.
2. Submit a decomposable task and a simple task.
3. Watch Activity and Task Timeline update; expand decision details.
4. Call the new events API with and without `includeChildren=true` and compare results.

### Notes

- Phase 1 prioritizes speed and compatibility; Phase 2 improves queryability. If desired, we can stay on Phase 1 longer and still meet observability needs.

