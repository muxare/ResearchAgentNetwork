<script lang="ts">
  import { serverEvents } from '$lib/stores/events';

  type ActivityItem = {
    ts: string;
    taskId: string;
    status?: string;
    eventType?: string;
    message?: string;
  };

  let { selectedTaskId, limit = 100 }: { selectedTaskId?: string | null; limit?: number } = $props();
  let items = $state<ActivityItem[]>([]);

  $effect(() => {
    const unsub = serverEvents.subscribe((msg: any) => {
      if (!msg) return;
      if (msg.type === 'task' && msg.TaskId) {
        const row: ActivityItem = {
          ts: new Date().toLocaleTimeString(),
          taskId: String(msg.TaskId),
          status: msg.Status,
          eventType: msg.EventType,
          message: msg.Message
        };
        items = [row, ...items].slice(0, limit);
      }
    });
    return () => unsub();
  });

  function filtered() {
    if (!selectedTaskId) return items;
    const id = String(selectedTaskId).toLowerCase();
    return items.filter(i => i.taskId.toLowerCase() === id);
  }
</script>

<div class="p-3 rounded border bg-white">
  <div class="flex items-center justify-between">
    <h3 class="text-sm font-semibold">Activity</h3>
    <div class="text-xs text-gray-500">{filtered().length}/{items.length}</div>
  </div>
  {#if items.length === 0}
    <div class="text-xs text-gray-500 mt-2">No recent activity.</div>
  {:else}
    <ul class="mt-2 space-y-1 max-h-64 overflow-auto text-xs text-gray-700">
      {#each filtered() as it}
        <li class="flex items-start gap-2">
          <span class="text-gray-500 shrink-0 w-20">{it.ts}</span>
          <span class="font-mono text-[10px] text-gray-500 shrink-0 w-36 truncate">{it.taskId}</span>
          <span class="shrink-0 w-20">{it.eventType}</span>
          <span class="shrink-0 w-24">{it.status}</span>
          <span class="truncate">{it.message}</span>
        </li>
      {/each}
    </ul>
  {/if}
</div>

