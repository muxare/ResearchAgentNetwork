<script lang="ts">
  import { onMount } from 'svelte';
  import KanbanBoard from '$lib/components/KanbanBoard.svelte';
  import TaskDetails from '$lib/components/TaskDetails.svelte';
  import SettingsPanel from '$lib/components/SettingsPanel.svelte';
  import StatusLegend from '$lib/components/StatusLegend.svelte';
  import NewTaskForm from '$lib/components/NewTaskForm.svelte';
  import { serverEvents } from '$lib/stores/events';
  import ActivityPanel from '$lib/components/ActivityPanel.svelte';
  type TaskItem = { id: string; description: string; status: string; createdAt?: string; priority?: number };
  let tasks = $state<TaskItem[]>([]);
  let filterText = $state('');
  let filterStatus = $state('');
  let selectedId = $state<string | null>(null);
  let connection = $state<'connecting' | 'connected' | 'reconnecting'>('connecting');
  let reconnectInMs = $state<number | null>(null);
  let loading = $state(true);
  let error = $state('');
  async function refreshTasks() {
    try {
      const res = await fetch('/api/tasks');
      if (!res.ok) throw new Error(await res.text());
      tasks = await res.json();
      loading = false;
    } catch (e:any) {
      error = e.message || 'Failed to load tasks';
      loading = false;
    }
  }
  onMount(() => { refreshTasks(); });

  function filtered(list: TaskItem[]) {
    const q = filterText.toLowerCase();
    return list.filter(t => (!filterStatus || t.status === filterStatus) && (!q || t.description.toLowerCase().includes(q) || (t.id ?? '').toLowerCase().includes(q)));
  }

  // Subscribe to centralized server events
  $effect(() => {
    const STATUS_NAMES = ['Pending','Analyzing','Executing','Aggregating','Completed','Failed'] as const;
    function toStatusName(val: any): string {
      if (typeof val === 'string') return val;
      if (typeof val === 'number') return STATUS_NAMES[val] ?? 'Pending';
      return 'Pending';
    }
    const unsub = serverEvents.subscribe((msg: any) => {
      if (!msg) return;
      if (msg.type === 'connection') {
        connection = msg.state ?? 'connecting';
        reconnectInMs = msg.state === 'reconnecting' ? (msg.retryMs ?? null) : null;
        return;
      }
      if (msg.type === 'task' && msg.TaskId) {
        const id = (msg.TaskId + '').toLowerCase();
        const idx = tasks.findIndex(t => (t.id + '').toLowerCase() === id);
        if (idx >= 0) {
          const prev = tasks[idx];
          const nextStatus = toStatusName(msg.Status);
          const statusChanged = prev.status !== nextStatus;
          const flashUntil = statusChanged ? Date.now() + 1500 : (prev as any).flashUntil;
          tasks[idx] = { ...(prev as any), status: nextStatus, flashUntil } as any;
          tasks = [...tasks];
        } else {
          fetch(`/api/tasks/${msg.TaskId}`)
            .then(r => (r.ok ? r.json() : null))
            .then((t) => {
              if (!t) return;
              const exists = tasks.some(x => (x.id + '').toLowerCase() === id);
              if (!exists) tasks = [...tasks, t];
            })
            .catch(() => {});
        }
      }
    });
    return () => unsub();
  });

  // Periodic safety refresh to recover from missed events
  onMount(() => {
    const h = setInterval(() => { refreshTasks(); }, 20000);
    return () => clearInterval(h);
  });
</script>

<main class="p-6 md:p-8 min-h-screen">
  <div class="flex items-baseline justify-between">
    <div>
      <h1 class="text-2xl md:text-3xl font-bold tracking-tight">RAN UI</h1>
      <p class="text-sm text-gray-600">SvelteKit + Tailwind — Kanban</p>
    </div>
  </div>

  <div class="mt-4 flex flex-col gap-4">
    <div class="flex items-center gap-3 bg-white/80 border rounded-xl p-2 shadow-sm text-xs">
      <div class={
        connection === 'connected' ? 'px-2 py-0.5 rounded bg-green-50 text-green-700 border border-green-200' :
        connection === 'reconnecting' ? 'px-2 py-0.5 rounded bg-yellow-50 text-yellow-700 border border-yellow-200' :
        'px-2 py-0.5 rounded bg-gray-50 text-gray-700 border border-gray-200'
      }>
        {connection === 'reconnecting' ? `Reconnecting${reconnectInMs ? ` in ~${Math.round(reconnectInMs/1000)}s` : '…'}` : connection === 'connected' ? 'Connected' : 'Connecting…'}
      </div>
      <div class="text-gray-600">Counts:</div>
      <div class="flex flex-wrap gap-1">
        {#each ['Pending','Analyzing','Executing','Aggregating','Completed','Failed'] as s}
          <span class="px-1 py-0.5 rounded border bg-gray-50 text-gray-700">{s}: {tasks.filter(t => t.status === s).length}</span>
        {/each}
      </div>
    </div>
    <div class="flex items-end gap-3 bg-white/80 border rounded-xl p-3 shadow-sm">
      <label class="text-sm">
        <div class="text-xs text-gray-600">Filter</div>
        <input class="border rounded-md px-2 py-1 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" bind:value={filterText} placeholder="search..." />
      </label>
      <label class="text-sm">
        <div class="text-xs text-gray-600">Status</div>
        <select class="border rounded-md px-2 py-1 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" bind:value={filterStatus}>
          <option value="">All</option>
          <option>Pending</option>
          <option>Analyzing</option>
          <option>Executing</option>
          <option>Aggregating</option>
          <option>Completed</option>
          <option>Failed</option>
        </select>
      </label>
    </div>

    {#if error}
      <div class="text-red-600 text-sm">{error}</div>
    {/if}

    <SettingsPanel />
    <StatusLegend />
    <NewTaskForm on:created={() => { refreshTasks(); }} />

    <div class="space-y-6">
      <div class="bg-white/80 border rounded-xl shadow-sm">
        <KanbanBoard tasks={filtered(tasks)} loading={loading} onselect={({ id }) => (selectedId = id)} />
      </div>
      <div class="bg-white/80 border rounded-xl p-3 shadow-sm">
        <TaskDetails taskId={selectedId} on:select={(e: CustomEvent<{ id: string }>) => (selectedId = e.detail.id)} />
      </div>
      <div class="bg-white/80 border rounded-xl p-3 shadow-sm">
        <ActivityPanel selectedTaskId={selectedId} />
      </div>
    </div>
  </div>
</main>
