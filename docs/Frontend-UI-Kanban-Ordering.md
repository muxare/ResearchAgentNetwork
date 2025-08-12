### Kanban ordering and status normalization

**Problem**
- When tasks changed status, the UI sometimes crashed with: `Cannot read properties of undefined (reading 'push')`.
- Root cause: grouping tasks by `status` assumed the status key existed in the order map. Unknown/mismatched statuses (e.g., lowercase, unexpected values) produced `undefined` buckets.

**Solution**
- Introduced `normalizeStatus(status: string): Status` and `VALID_STATUSES` in `ui/app/src/lib/utils/order.ts`.
- Used normalization in both:
  - `reconcileOrder(...)` when grouping task IDs
  - `KanbanBoard.byStatus(...)` when filtering tasks per column

Key edits:
```ts
// ui/app/src/lib/utils/order.ts
export const VALID_STATUSES = ['Pending','Analyzing','Executing','Aggregating','Completed','Failed'] as const;
export function normalizeStatus(status: string): Status {
  return (VALID_STATUSES as readonly string[]).includes(status) ? (status as Status) : 'Pending';
}
// In reconcileOrder: use normalizeStatus for the grouping key
```

```svelte
<!-- ui/app/src/lib/components/KanbanBoard.svelte -->
import { normalizeStatus, VALID_STATUSES } from '$lib/utils/order';
const statuses = VALID_STATUSES as string[];
function byStatus(status: string) {
  const key = normalizeStatus(status);
  const list = tasks.map(t => ({ ...t, status: normalizeStatus(t.status) })).filter(t => t.status === key);
  return orderTasksForStatus(key as any, list, order);
}
```

**Architecture/Data flow**
- The order map is the single source of truth for column item ordering, persisted in `localStorage`.
- Tasks from the backend are normalized to the fixed status set before grouping/sorting, ensuring stable keys and avoiding undefined buckets.

**Execution flow**
1. Tasks load or update via SSE.
2. `KanbanBoard` reconciles the persisted order with current tasks using normalized statuses.
3. Columns render tasks ordered by the reconciled order.

**How to test**
- Start the UI, load tasks, and drag cards between columns; no errors should appear in the console.
- Simulate SSE updates with unexpected statuses; cards should fall back to the `Pending` column without crashes.

**Notes**
- Column titles derive from `VALID_STATUSES`, keeping titles and keys in sync.
