<script lang="ts">
  import type { TaskItem } from '$lib/types/tasks';
  import { STATUS_NAMES } from '$lib/constants/statuses';

  export let connection: 'connecting' | 'connected' | 'reconnecting' = 'connecting';
  export let reconnectInMs: number | null = null;
  export let tasks: TaskItem[] = [];

  $: connectionClass =
    connection === 'connected'
      ? 'px-2 py-0.5 rounded bg-green-50 text-green-700 border border-green-200'
      : connection === 'reconnecting'
      ? 'px-2 py-0.5 rounded bg-yellow-50 text-yellow-700 border border-yellow-200'
      : 'px-2 py-0.5 rounded bg-gray-50 text-gray-700 border border-gray-200';

  $: connectionText =
    connection === 'reconnecting'
      ? `Reconnecting${reconnectInMs ? ` in ~${Math.round(reconnectInMs/1000)}s` : '…'}`
      : connection === 'connected'
      ? 'Connected'
      : 'Connecting…';
</script>

<div class="flex items-center gap-3 bg-white/80 border rounded-xl p-2 shadow-sm text-xs">
  <div class={connectionClass}>{connectionText}</div>
  <div class="text-gray-600">Counts:</div>
  <div class="flex flex-wrap gap-1">
    {#each STATUS_NAMES as s}
      <span class="px-1 py-0.5 rounded border bg-gray-50 text-gray-700">{s}: {tasks.filter(t => t.status === s).length}</span>
    {/each}
  </div>
</div>

