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

### Files

- `ui/app/src/routes/+page.svelte`
  - `startSse()` handles `onopen`, `onmessage`, `onerror` with backoff reconnect.
  - Unknown tasks trigger a fetch of `/api/tasks/{id}` and append.
  - A 20s `setInterval` calls `refreshTasks()` as a safety net.
- `ui/app/src/lib/components/KanbanBoard.svelte`
  - Reconciles local order when `tasks` change, preserving manual ordering across updates.
- `ui/app/src/lib/components/TaskDetails.svelte`
  - Keeps its own SSE for now (will be centralized in a later phase) to display per-task live log lines.

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

