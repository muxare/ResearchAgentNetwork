import { writable, get } from 'svelte/store';
import type { TaskItem } from '$lib/types/tasks';
import { serverEvents } from '$lib/stores/events';
import { toStatusName } from '$lib/utils/status';

export const tasks = writable<TaskItem[]>([]);
export const loading = writable<boolean>(false);
export const error = writable<string>('');
export const connection = writable<'connecting' | 'connected' | 'reconnecting'>('connecting');
export const reconnectInMs = writable<number | null>(null);

let started = false;
let unsubscribeEvents: (() => void) | null = null;
let refreshHandle: any = null;

export function setInitialTasks(initial: TaskItem[] | null | undefined): void {
  if (Array.isArray(initial)) {
    tasks.set(initial);
    loading.set(false);
    error.set('');
  }
}

export async function refreshTasks(): Promise<void> {
  loading.set(true);
  try {
    const res = await fetch('/api/tasks');
    if (!res.ok) throw new Error(await res.text());
    const list = (await res.json()) as any[];
    const shaped = list.map((t: any) => ({
      ...t,
      isSystemTask: !!t.isSystemTask,
      category: t.category ?? (t.metadata?.Category ?? t.metadata?.category ?? ''),
    })) as TaskItem[];
    tasks.set(shaped);
    error.set('');
  } catch (e: any) {
    error.set(e?.message || 'Failed to load tasks');
  } finally {
    loading.set(false);
  }
}

export function start(): void {
  if (started) return;
  started = true;

  // Subscribe to centralized server events
  unsubscribeEvents = serverEvents.subscribe((msg: any) => {
    if (!msg) return;
    if (msg.type === 'connection') {
      connection.set(msg.state ?? 'connecting');
      reconnectInMs.set(msg.state === 'reconnecting' ? (msg.retryMs ?? null) : null);
      return;
    }
    if (msg.type === 'task' && msg.TaskId) {
      const idLower = String(msg.TaskId).toLowerCase();
      const current = get(tasks);
      const idx = current.findIndex(t => String(t.id).toLowerCase() === idLower);
      if (idx >= 0) {
        const prev: any = current[idx] as any;
        const nextStatus = toStatusName(msg.Status);
        const statusChanged = prev.status !== nextStatus;
        const flashUntil = statusChanged ? Date.now() + 1500 : prev.flashUntil;
        const next = [...current];
        next[idx] = { ...prev, status: nextStatus, flashUntil } as any;
        tasks.set(next);
      } else {
        fetch(`/api/tasks/${msg.TaskId}`)
          .then(r => (r.ok ? r.json() : null))
          .then((t) => {
            if (!t) return;
            const exists = get(tasks).some(x => String((x as any).id).toLowerCase() === idLower);
            if (!exists) {
              const shaped = { ...t, isSystemTask: !!(t as any).isSystemTask, category: (t as any).category ?? ((t as any).metadata?.Category ?? (t as any).metadata?.category ?? '') } as any;
              tasks.set([...get(tasks), shaped]);
            }
          })
          .catch(() => {});
      }
    }
  });

  // Periodic safety refresh to recover from missed events
  refreshHandle = setInterval(() => { refreshTasks(); }, 20000);
}

export function stop(): void {
  if (!started) return;
  started = false;
  try { if (unsubscribeEvents) unsubscribeEvents(); } catch {}
  unsubscribeEvents = null;
  if (refreshHandle) clearInterval(refreshHandle);
  refreshHandle = null;
}

