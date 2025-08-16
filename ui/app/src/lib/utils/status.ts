import { STATUS_NAMES } from '$lib/constants/statuses';

export function toStatusName(val: unknown): string {
  if (typeof val === 'string') return val;
  if (typeof val === 'number') return (STATUS_NAMES as readonly string[])[val] ?? 'Pending';
  return 'Pending';
}

