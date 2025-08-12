import { readable } from 'svelte/store';

export type ServerEvent = {
  type: string;
  [key: string]: any;
} | null;

let sse: EventSource | null = null;
let reconnectHandle: any = null;
let retryMs = 1000;
const maxRetryMs = 30000;

export const serverEvents = readable<ServerEvent>(null, (set) => {
  function cleanup() {
    try { sse?.close(); } catch {}
    sse = null;
    if (reconnectHandle) clearTimeout(reconnectHandle);
    reconnectHandle = null;
  }

  function start() {
    try {
      sse = new EventSource('/api/events');
      sse.onopen = () => {
        retryMs = 1000;
        // emit connection state
        set({ type: 'connection', state: 'connected' });
      };
      sse.onmessage = (ev) => {
        try {
          const msg = JSON.parse(ev.data);
          set(msg);
        } catch {
          // ignore non-JSON (e.g., heartbeat comments)
        }
      };
      sse.onerror = () => {
        cleanup();
        // emit reconnecting state
        set({ type: 'connection', state: 'reconnecting', retryMs });
        reconnectHandle = setTimeout(() => {
          start();
        }, retryMs);
        retryMs = Math.min(retryMs * 2, maxRetryMs);
      };
    } catch {
      reconnectHandle = setTimeout(() => start(), retryMs);
      retryMs = Math.min(retryMs * 2, maxRetryMs);
    }
  }

  start();

  return () => {
    cleanup();
  };
});

