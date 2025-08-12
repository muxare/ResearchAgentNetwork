import { writable } from 'svelte/store';

export type ToastType = 'success' | 'error' | 'info';
export type Toast = { id: number; message: string; type: ToastType };

export const toasts = writable<Toast[]>([]);

export function showToast(message: string, type: ToastType = 'info', durationMs = 2000): void {
  const id = Date.now() + Math.floor(Math.random() * 1000);
  toasts.update((list) => [...list, { id, message, type }]);
  if (durationMs > 0) {
    setTimeout(() => removeToast(id), durationMs);
  }
}

export function removeToast(id: number): void {
  toasts.update((list) => list.filter((t) => t.id !== id));
}

