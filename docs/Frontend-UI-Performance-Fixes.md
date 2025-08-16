## UI performance and stability fixes: avoiding reactive effect loops (Svelte)

### What changed

- Replaced reactive `$effect` blocks that subscribed to streams or performed one-time initialization with `onMount` to avoid re-running on state changes:
  - `ui/app/src/routes/+page.svelte`: initialize tasks once per page mount (start/stop background sync).
  - `ui/app/src/lib/components/ActivityPanel.svelte`: subscribe to `serverEvents` once; previously the effect referenced `items`, causing re-subscribe on every update.
  - `ui/app/src/lib/components/KanbanColumn.svelte`: attach `finalize` listener once per mount.
  - `ui/app/src/lib/components/TaskDetails.svelte`: guard data reload to only when `taskId` changes; move event subscription to `onMount`.

### Why

- In Svelte 5, `$effect` re-runs whenever any variables referenced inside the effect change. Some effects referenced local state they updated (`items`, `live`, etc.). That created a feedback loop (effect → update → effect), leading to repeated subscriptions/handlers, high memory usage, and the runtime error: `effect_update_depth_exceeded`.

### Architecture / data flow impact

- Subscriptions to the shared `serverEvents` store are now lifecycle-scoped (mounted → subscribe, unmounted → unsubscribe), not reactive to unrelated state changes.
- Page-level initialization (`tasks` seeding and background sync `start()`/`stop()`) is now strictly one-time per mount.
- Ordering reconciliation in `KanbanBoard.svelte` remains reactive, but only mutates local `order` when it actually changes.

### Execution flow

1. `+page.svelte` mounts → seeds initial tasks from page data → calls `start()` (SSE + periodic refresh). On unmount → `stop()`.
2. `ActivityPanel.svelte` mounts → subscribes to `serverEvents`; incoming `task` messages push a single row into `items` (bounded by `limit`). No re-subscribe on each push.
3. `TaskDetails.svelte` updates:
   - When `taskId` changes → fetch metadata/report/children once.
   - Live events subscription established once on mount; uses current `taskId` to filter.
4. `KanbanColumn.svelte` mounts → sets up a single `finalize` handler for drag/drop.

### How to test

- Start the UI and open the Kanban page.
- Create a few tasks and let backend emit frequent SSE `task` events.
- Observe DevTools Console: the previous error `effect_update_depth_exceeded` should no longer appear.
- Observe Memory/Performance:
  - Event listeners count should remain stable while activity updates (no linear growth over time).
  - Heap usage should not grow unbounded while the page idles.

### Notes / follow-ups

- The shared `now` store ticks at 250ms for card highlighting; adjust if needed for lower CPU usage.
- If further loops appear, search for `$effect` blocks that both use and update the same reactive values or stores; convert those cases to `onMount` or guard the effect inputs.

## Frontend UI Performance Fixes (Kanban + Task Details)

This document captures targeted UI changes to prevent freezes when clicking a Kanban task card and other minor performance risks.

### Summary of Changes

- Shared time store for task flash highlighting to remove many per-card timers.
  - Added `ui/app/src/lib/stores/now.ts` and updated `TaskCard.svelte` to use `$now` instead of creating a `setInterval` per card.
- Lazy, idle-time markdown parsing in Task Details.
  - `TaskDetails.svelte` now defers parsing report markdown until the Report tab is opened and performs parsing during idle time. This avoids blocking the UI on initial click.
- Bounded Activity feed size.
  - `+page.svelte` passes `limit={300}` to `ActivityPanel` to cap list growth and re-render cost.

### Architecture & Data Flow

- Kanban columns render `TaskCard` entries derived from the `tasks` store. Clicking a card sets `selectedId` and opens the modal.
- `TaskDetailsModal` renders `TaskDetails` with the current `taskId`.
- `TaskDetails` loads metadata, children, and the report text. Markdown parsing is now deferred and performed only when the Report tab is visible.
- Live updates stream via `serverEvents`, updating both `tasks` and activity items.
- The `now` store ticks every 250ms centrally; `TaskCard` subscribes to `$now` to toggle flash rings, avoiding per-card timers.

### Execution Flow on Task Click

1. User clicks a `TaskCard`.
2. `+page.svelte` sets `selectedId` and `showDetails=true`.
3. Modal opens with `TaskDetails(taskId)`.
4. `TaskDetails` loads: task metadata, report text (as raw string), and children.
5. Parsing of markdown does not happen until the user switches to the Report tab. When the tab is active, parsing runs in idle time.

### Risks Addressed

- Removing per-card intervals prevents timer proliferation (many cards → many intervals) that can cause frame drops and jank.
- Deferring markdown parsing prevents main-thread stalls when reports are large.
- Activity list cap avoids unbounded render cost over long sessions.

### How to Test

1. Open the app and create or load many tasks (100+).
2. Click several task cards rapidly:
   - Modal should open instantly without UI freeze.
   - Status ring flashes should still work across many cards.
3. Open Task Details → switch between tabs:
   - Switching to Report should render within a reasonable time; other tabs should not lag.
4. Keep the app running during active server events:
   - Observe Activity panel count capped at 300. Scrolling remains responsive.

### Future Improvements (Optional)

- Move markdown parsing to a Web Worker for very large reports.
- Precompute per-status counts and display in `StatusBar` without repeated `filter` in the template.
- Memoize or precompute `KanbanBoard.byStatus` results per render to reduce repeated work when unrelated state (like modal visibility) changes.

