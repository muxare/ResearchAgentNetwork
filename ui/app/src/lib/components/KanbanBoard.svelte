<script lang="ts">
  import KanbanColumn from './KanbanColumn.svelte';
  import type { TaskItem } from './TaskCard.svelte';
  import { loadOrder, saveOrder, reconcileOrder, orderTasksForStatus, normalizeStatus, VALID_STATUSES } from '$lib/utils/order';

  let { tasks, loading, onselect, laneHeight }:
    { tasks: TaskItem[]; loading?: boolean; onselect?: (detail: { id: string }) => void; laneHeight?: string } = $props();

  const statuses = VALID_STATUSES as string[];
  let order = $state(loadOrder());
  let filterStatus = $state<string>('');
  let filterText = $state<string>('');
  let laneHeightLocal = $state<string>('62vh');
  $effect(() => {
    try {
      const a = localStorage.getItem('kanbanLaneHeight');
      if (a) laneHeightLocal = a;
      const b = localStorage.getItem('kanbanFilterStatus');
      if (b) filterStatus = b;
      const c = localStorage.getItem('kanbanFilterText');
      if (c) filterText = c;
    } catch {}
  });
  function saveLaneHeight() {
    try { localStorage.setItem('kanbanLaneHeight', laneHeightLocal); } catch {}
  }
  function saveFilterStatus() {
    try { localStorage.setItem('kanbanFilterStatus', filterStatus); } catch {}
  }
  function saveFilterText() {
    try { localStorage.setItem('kanbanFilterText', filterText); } catch {}
  }

  const STATUS_NAMES = ['Pending','Analyzing','Executing','Aggregating','Completed','Failed'] as const;
  function toStatusName(val: any): string {
    if (typeof val === 'string') return val;
    if (typeof val === 'number') return STATUS_NAMES[val] ?? 'Pending';
    return 'Pending';
  }

  type DecoratedTask = TaskItem & { depth?: number; isRoot?: boolean };
  function byStatus(status: string): DecoratedTask[] {
    const key = normalizeStatus(status);
    const q = filterText.trim().toLowerCase();
    let source = tasks;
    if (filterStatus) source = source.filter(t => normalizeStatus(toStatusName((t as any).status)) === filterStatus);
    if (q) source = source.filter(t => (t.description?.toLowerCase().includes(q)) || ((t.id + '').toLowerCase().includes(q)));
    const list = source.map(t => ({ ...t, status: normalizeStatus(toStatusName((t as any).status)) })).filter(t => t.status === key);
    // decorate with hierarchy info if available
    const parentById: Record<string, string | undefined> = {};
    for (const t of tasks as any) parentById[(t as any).id] = (t as any).parentTaskId;
    const depthMemo: Record<string, number> = {};
    function depthFor(id: string, guard: Set<string> = new Set()): number {
      if (depthMemo[id] !== undefined) return depthMemo[id];
      if (guard.has(id)) return 0;
      guard.add(id);
      const p = parentById[id];
      if (!p) return (depthMemo[id] = 0);
      return (depthMemo[id] = 1 + depthFor(p, guard));
    }
    const decorated: DecoratedTask[] = list.map((t: any) => {
      const d = depthFor(t.id);
      return { ...t, depth: d, isRoot: !parentById[t.id] };
    });
    return orderTasksForStatus<DecoratedTask>(key as any, decorated, order);
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
  <div class="p-2 border-b bg-white/70 backdrop-blur sticky top-0 z-10">
    <div class="flex flex-wrap items-end gap-3 text-xs">
      <label class="text-sm">
        <div class="text-[11px] text-gray-600">Filter</div>
        <input class="border rounded-md px-2 py-1 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" bind:value={filterText} oninput={saveFilterText} placeholder="search..." />
      </label>
      <label class="text-sm">
        <div class="text-[11px] text-gray-600">Status</div>
        <select class="border rounded-md px-2 py-1 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" bind:value={filterStatus} onchange={saveFilterStatus}>
          <option value="">All</option>
          {#each statuses as s}
            <option value={s}>{s}</option>
          {/each}
        </select>
      </label>
      <label class="text-sm">
        <div class="text-[11px] text-gray-600">Lane Height</div>
        <input class="border rounded-md px-2 py-1 w-28 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" bind:value={laneHeightLocal} onchange={saveLaneHeight} placeholder="62vh or 480px" />
      </label>
    </div>
  </div>

  <div class="flex flex-wrap items-start gap-3 p-2 w-full">
    {#each statuses as s}
      <KanbanColumn
        title={s}
        tasks={byStatus(s)}
        loading={loading}
        onselect={(detail) => onselect?.(detail)}
        laneHeight={laneHeight ?? laneHeightLocal}
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

