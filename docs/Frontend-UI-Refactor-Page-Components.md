### Frontend UI: Page Components Refactor (Phase 1)

**Goal**: Improve readability of `+page.svelte` by extracting presentational components without changing functionality.

### Overview

The page now orchestrates state and effects, while new components render previously inline UI blocks:

- `PageHeader.svelte`: Title and subtitle header.
- `StatusBar.svelte`: Connection status + per-status counts.
- `ErrorBanner.svelte`: Error message display.
- `TaskDetailsModal.svelte`: Modal wrapper containing `TaskDetails` with backdrop and close control.

Shared:
- `lib/types/tasks.ts`: `TaskItem` type.
- `lib/constants/statuses.ts`: `STATUS_NAMES` constant.

### Data flow

- `+page.svelte` maintains all state: `tasks`, `loading`, `error`, `selectedId`, `showDetails`, `connection`, `reconnectInMs`.
- `StatusBar` receives `connection`, `reconnectInMs`, `tasks` as props and computes UI-only labels.
- `TaskDetailsModal` receives `open`, `taskId` and emits `close` and `select` events. Parent updates `showDetails`/`selectedId` as before.
- No logic was moved out of the page aside from rendering concerns. Event subscriptions and refresh timers remain in the page.

### How to test

1. Load the page; verify header shows and no layout changes beyond component boundaries.
2. Confirm connection tag and counts render and update when tasks change.
3. Trigger an error (e.g., stop API) and verify `ErrorBanner` appears.
4. Create a task via `NewTaskForm`; board populates; click a task card → modal opens; close via backdrop/Close button.
5. Selecting a related task inside `TaskDetails` updates the modal via the `select` event.

### Future improvements (optional phases)

- Extract `toStatusName` and any status helpers into `lib/utils/status.ts` or reuse `constants/statuses.ts` if we later consolidate logic.
- Introduce a `Card.svelte` wrapper to standardize panels.
- Consider a `TasksStore` to encapsulate fetch/subscription logic and reduce page script size.

