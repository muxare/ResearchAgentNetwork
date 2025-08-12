<script lang="ts">
  import KanbanColumn from './KanbanColumn.svelte';
  import type { TaskItem } from './TaskCard.svelte';
  import { loadOrder, saveOrder, reconcileOrder, orderTasksForStatus, normalizeStatus, VALID_STATUSES } from '$lib/utils/order';

  let { tasks, loading, onselect }: { tasks: TaskItem[]; loading?: boolean; onselect?: (detail: { id: string }) => void } = $props();

  const statuses = VALID_STATUSES as string[];
  let order = $state(loadOrder());

  function byStatus(status: string) {
    const key = normalizeStatus(status);
    const list = tasks.map(t => ({ ...t, status: normalizeStatus(t.status) })).filter(t => t.status === key);
    return orderTasksForStatus(key as any, list, order);
  }

  function ordersEqual(a: Record<string, string[]>, b: Record<string, string[]>) {
    for (const s of statuses) {
      const aa = a[s] || [];
      const bb = b[s] || [];
      if (aa.length !== bb.length) return false;
      for (let i = 0; i < aa.length; i++) if (aa[i] !== bb[i]) return false;
    }
    return true;
  }

  $effect(() => {
    // reconcile order whenever tasks change (SSE or initial load)
    const next = reconcileOrder(order as any, tasks as any);
    if (!ordersEqual(order as any, next as any)) {
      order = next as any;
      saveOrder(order as any);
    }
  });
</script>

<div class="overflow-x-hidden">
  <div class="flex flex-wrap items-start gap-3 p-2 w-full">
    {#each statuses as s}
      <KanbanColumn
        title={s}
        tasks={byStatus(s)}
        loading={loading}
        onselect={(detail) => onselect?.(detail)}
        onreorder={(detail) => {
          const { status, ids } = detail as any;
          order = { ...order, [status]: ids } as any;
          saveOrder(order as any);
        }}
      />
    {/each}
  </div>
  {#if tasks.length === 0}
    <div class="text-sm text-gray-500 p-2">No tasks</div>
  {/if}
</div>

