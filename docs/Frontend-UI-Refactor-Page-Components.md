# Frontend UI: Page Components Refactor (Phase 1)

**Goal**: Improve readability of `+page.svelte` by extracting presentational components without changing functionality.

## Overview

The page now orchestrates state and effects, while new components render previously inline UI blocks:

- `PageHeader.svelte`: Title and subtitle header.
- `StatusBar.svelte`: Connection status + per-status counts.
- `ErrorBanner.svelte`: Error message display.
- `TaskDetailsModal.svelte`: Modal wrapper containing `TaskDetails` with backdrop and close control.

Shared:

- `lib/types/tasks.ts`: `TaskItem` type.
- `lib/constants/statuses.ts`: `STATUS_NAMES` constant.

## Data flow

- `+page.svelte` maintains all state: `tasks`, `loading`, `error`, `selectedId`, `showDetails`, `connection`, `reconnectInMs`.
- `StatusBar` receives `connection`, `reconnectInMs`, `tasks` as props and computes UI-only labels.
- `TaskDetailsModal` receives `open`, `taskId` and emits `close` and `select` events. Parent updates `showDetails`/`selectedId` as before.
- No logic was moved out of the page aside from rendering concerns. Event subscriptions and refresh timers remain in the page.

## How to test

1. Load the page; verify header shows and no layout changes beyond component boundaries.
2. Confirm connection tag and counts render and update when tasks change.
3. Trigger an error (e.g., stop API) and verify `ErrorBanner` appears.
4. Create a task via `NewTaskForm`; board populates; click a task card → modal opens; close via backdrop/Close button.
5. Selecting a related task inside `TaskDetails` updates the modal via the `select` event.

## Phase 2: Status utilities centralization

- Added `lib/utils/status.ts` with `toStatusName()` for consistent status normalization.
- Refactored usages in `+page.svelte`, `KanbanBoard.svelte`, `TaskDetails.svelte`, and `ActivityPanel.svelte` to import the shared util.
- Kept `STATUS_NAMES` in `lib/constants/statuses.ts` for display contexts like `StatusBar`.

## Phase 3: Tasks store extraction

Centralized task data orchestration into a store to reduce page complexity and standardize side effects.

Exports from `lib/stores/tasks.ts`:

- `tasks`: Svelte store of `TaskItem[]`
- `loading`: Svelte store of `boolean`
- `error`: Svelte store of `string`
- `connection`: Svelte store of `'connecting' | 'connected' | 'reconnecting'`
- `reconnectInMs`: Svelte store of `number | null`
- `setInitialTasks(initial: TaskItem[])`: seed initial tasks from `+page.ts`
- `refreshTasks()`: fetches `/api/tasks` and updates stores
- `start() / stop()`: attach/detach SSE subscription and periodic refresh (20s)

Responsibilities:

- Subscribe to `serverEvents` and update connection state and tasks on `task` events
- Normalize statuses via `toStatusName()`
- Preserve UI flash behavior when status changes
- Periodic refresh to recover from missed events

Page updates:

- Initialize store in `+page.svelte` with `setInitialTasks(data.tasks)` and call `start()`/`stop()` in a lifecycle effect
- Bind UI to `$tasksStore`, `$loadingStore`, `$errorStore`, `$connectionStore`, `$reconnectStore`
- Keep UI-only state (`filterText`, `selectedId`, `showDetails`) local to the page

How to test:

1. Load the page; tasks should appear as before, without flicker
2. Confirm live updates still move cards and status counts update
3. Temporarily break the API to see the error banner appear
4. Create a task; after success, the board refreshes
5. Verify that closing/opening the page re-establishes SSE and refresh timer

## Future improvements (optional)

- Introduce a `Card.svelte` wrapper to standardize panels.
- Consider a `TasksStore` to encapsulate fetch/subscription logic and reduce page script size.
