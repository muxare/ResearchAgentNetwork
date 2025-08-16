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
 
### Completed column stacked rendering

We render an illusion of a stacked card when a parent task and all its subtasks are completed. The parent appears once in the Completed column with a subtle offset “stack” behind it, and a (+N) indicator where N is the number of completed subtasks hidden behind the parent.

Behavior:

- A task is considered a parent if it has `parentTaskId` undefined or null, and there are one or more tasks with `parentTaskId` equal to its `id`.
- In the `Completed` column only, if a parent and all of its children are also completed, we render a single parent card.
- The parent card shows a visual stack (two offset backgrounds) and a `(+N)` indicator showing the number of completed subtasks.
- The completed child cards are hidden from the `Completed` column list to keep the UI compact.

Implementation notes:

- Grouping and collapsing is handled in `ui/app/src/lib/components/KanbanBoard.svelte` inside `byStatus('Completed')`.
- We compute `childrenByParent` from the full task list, determine parents whose children are all completed, and annotate such parent cards with `_stackCount` while filtering out the children from the Completed list.
- The stacked illusion and `(+N)` badge are rendered in `ui/app/src/lib/components/TaskCard.svelte` when `_stackCount > 0`.

How to test:

1. Create a parent task P and subtasks C1, C2 linked via `parentTaskId: P.id`.
2. Mark C1 and C2 as Completed; keep P not completed yet → C1 and C2 should appear normally in their appropriate columns.
3. Mark P as Completed → In the Completed column, C1 and C2 should be hidden; P should display once with a subtle stacked background and a `(+2)` indicator.
4. If any child is not completed, P should not display as a stack and children should remain individually visible.

Limitations:

- This behavior only applies in the `Completed` column. Other columns display all tasks individually.
- `_stackCount` is a transient UI-only field added in-memory; it is not persisted.
 
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

### Activity panel item limit change

- Previously, `ui/app/src/lib/components/ActivityPanel.svelte` defaulted to a `limit` of 100 items and sliced the list accordingly.
- Now, `limit` is optional. If omitted, the panel retains all activities for the session. If provided (finite), the list is capped to that value.
- Trade-off: keeping all items increases memory usage for very long sessions; pass a `limit` to bound it when needed.

How to test:
1. Start backend and UI.
2. Generate more than 100 task events.
3. Verify the Activity panel shows all entries beyond 100.
4. Optionally render `<ActivityPanel limit={200} />` and confirm the cap applies at 200.

