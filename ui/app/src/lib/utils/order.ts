export type Status = 'Pending' | 'Analyzing' | 'Executing' | 'Aggregating' | 'Completed' | 'Failed';

const STORAGE_KEY = 'kanbanOrderV1';

export const VALID_STATUSES: ReadonlyArray<Status> = [
  'Pending',
  'Analyzing',
  'Executing',
  'Aggregating',
  'Completed',
  'Failed'
];

export function normalizeStatus(status: string): Status {
  if ((VALID_STATUSES as ReadonlyArray<string>).includes(status)) {
    return status as Status;
  }
  // Fallback for unknown statuses
  return 'Pending';
}

export function loadOrder(): Record<Status, string[]> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return getEmptyOrder();
    const parsed = JSON.parse(raw);
    return { ...getEmptyOrder(), ...parsed };
  } catch {
    return getEmptyOrder();
  }
}

export function saveOrder(order: Record<Status, string[]>): void {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(order));
  } catch {}
}

export function getEmptyOrder(): Record<Status, string[]> {
  return {
    Pending: [],
    Analyzing: [],
    Executing: [],
    Aggregating: [],
    Completed: [],
    Failed: []
  };
}

export function reconcileOrder(
  order: Record<Status, string[]>,
  tasks: { id: string; status: Status }[]
): Record<Status, string[]> {
  const next = getEmptyOrder();
  // Group tasks by status
  const grouped: Record<Status, string[]> = getEmptyOrder();
  for (const t of tasks) {
    const key = normalizeStatus((t.status as unknown) as string);
    grouped[key].push(t.id);
  }
  // Preserve existing order where possible, then append new ids
  (Object.keys(next) as Status[]).forEach((status) => {
    const ids = new Set(grouped[status]);
    const preserved = (order[status] || []).filter((id) => ids.has(id));
    const missing = grouped[status].filter((id) => !preserved.includes(id));
    next[status] = [...preserved, ...missing];
  });
  return next;
}

export function orderTasksForStatus<T extends { id: string }>(
  status: Status,
  tasks: T[],
  order: Record<Status, string[]>
): T[] {
  const index = new Map(order[status].map((id, i) => [id, i] as const));
  return [...tasks].sort((a, b) => {
    const ia = index.has(a.id) ? (index.get(a.id) as number) : Number.MAX_SAFE_INTEGER;
    const ib = index.has(b.id) ? (index.get(b.id) as number) : Number.MAX_SAFE_INTEGER;
    return ia - ib;
  });
}

