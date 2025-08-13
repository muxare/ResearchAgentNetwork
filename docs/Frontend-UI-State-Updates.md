## Frontend State Updates (SSE + Polling)

This UI uses Server-Sent Events (SSE) for near-real-time updates of task status and creation (including subtasks from decomposition), with a lightweight polling fallback.

### Data flow

- Backend publishes task lifecycle events via `/api/events` (SSE). Each event payload looks like:
  - `{ type: "task", TaskId, Status, EventType, ParentTaskId, Message, TimestampUtc }`
  - An initial `{ type: "progress", summary }` snapshot is sent on connect.
- Frontend opens a single `EventSource` in `+page.svelte` and updates local state:
  - If `TaskId` exists in the current list → update its `status` in place.
  - If `TaskId` is unknown → fetch `/api/tasks/{id}` and append it (covers subtasks).
- A periodic `refreshTasks()` runs every 20s to reconcile state in case of missed messages.
- SSE has auto-reconnect with exponential backoff (1s → 30s).
 - Connection state events are emitted (`{ type: 'connection', state: 'connected'|'reconnecting' }`) and shown in a small banner together with per-status counts.

### Files

- `ui/app/src/lib/stores/events.ts`
  - Owns the single `EventSource` to `/api/events` and exposes a readable `serverEvents` store.
  - Handles reconnection with exponential backoff and ignores heartbeat comments.
- `ui/app/src/routes/+page.svelte`
  - Subscribes to `serverEvents` to upsert tasks (update status or fetch-and-append unknown tasks).
  - A 20s `setInterval` calls `refreshTasks()` as a safety net.
  - Displays a connection status indicator and live counts per status.
- `ui/app/src/lib/components/KanbanBoard.svelte`
  - Reconciles local order when `tasks` change, preserving manual ordering across updates.
- `ui/app/src/lib/components/TaskDetails.svelte`
  - Subscribes to `serverEvents` and appends human-readable log lines for the selected task.
  - Shows parent/children relationships and emits a `select` event to navigate between tasks.
 - `ui/app/src/lib/components/ActivityPanel.svelte`
  - Displays a live stream of recent task events. Can filter by selected task.
 - `ui/app/src/lib/components/TaskCard.svelte`
  - Briefly highlights a card when its status changes.
 
### Activity verification hints

The Activity panel includes a guidance column that explains how to verify each event:

- submitted: Open Task Details to track status
- status → analyzing: Watch for decomposition or direct execution
- status → executing: Wait for completed; then open report
- status → aggregating: Open parent task; synthesized report incoming
- decomposed: Open parent Task Details; verify subtasks listed
- retrieved: Check message for retrieved count; expect richer report
- ingested: Web results ingested; sources may appear in report
- completed: Open Task Details → Report and Sources
- failed: Open Task Details → consider Retry/Force Execute
- aggregated: Open parent Task Details → synthesized report
- stored: Stored in memory; future similar tasks may reuse
- skipped: No storage; review report manually
- refined: Report updated; refresh Task Details
- retry: Re-queued; watch for status updates
- merged: Check target task description for "(merged similar request)"

Implemented via `getVerifyHint()` in `ui/app/src/lib/components/ActivityPanel.svelte` mapping `EventType` and `Status` to concise guidance.

### How to test

1. Start the backend and UI.
2. Submit a task that decomposes and one that executes directly.
3. Observe Activity panel entries; ensure the guidance column suggests relevant verification steps for each event.
4. Open Task Details for the referenced task to validate the suggested action.

### Architecture implications

- No SignalR; SSE is sufficient and simpler. One-way push from server to client.
- UI state is optimistic and eventually consistent. Periodic refresh eliminates drift.
- Subtasks appear automatically as they are created.

### Testing

1) Start the backend (`ResearchAgentNetwork.Web`).
2) Start the UI (`ui/app`) via `npm run dev`.
3) Create a task in the UI. Observe it appear under Pending.
4) As the orchestrator decomposes tasks, new subtasks should pop into the board without a manual refresh.
5) Temporarily stop the backend and restart it. The UI should reconnect automatically; after reconnection, the 20s refresh should reconcile any missing items.
6) Open a task and watch the "Live Events" log update in near-real time.

### Future improvements (planned in Phase 2–3)

- Backend: add SSE heartbeat (comment lines) and headers to improve robustness behind proxies.
- Frontend: centralize SSE into a shared store to avoid multiple connections.

