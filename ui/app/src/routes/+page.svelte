<script lang="ts">
  import { onMount } from 'svelte';
  import KanbanBoard from '$lib/components/KanbanBoard.svelte';
  import TaskDetails from '$lib/components/TaskDetails.svelte';
  import SettingsPanel from '$lib/components/SettingsPanel.svelte';
  import StatusLegend from '$lib/components/StatusLegend.svelte';
  import NewTaskForm from '$lib/components/NewTaskForm.svelte';
  type TaskItem = { id: string; description: string; status: string; createdAt?: string; priority?: number };
  let tasks = $state<TaskItem[]>([]);
  let filterText = $state('');
  let filterStatus = $state('');
  let selectedId = $state<string | null>(null);
  let sse: EventSource | null = null;
  let reconnectHandle: any = null;
  let retryMs = 1000;
  const maxRetryMs = 30000;
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

  function startSse() {
    try {
      sse = new EventSource('/api/events');
      sse.onopen = () => {
        // reset backoff on successful connect
        retryMs = 1000;
      };
      sse.onmessage = (ev) => {
        try {
          const msg = JSON.parse(ev.data);
          if (msg.type === 'task' && msg.TaskId) {
            const id = (msg.TaskId + '').toLowerCase();
            const idx = tasks.findIndex(t => (t.id + '').toLowerCase() === id);
            if (idx >= 0) {
              tasks[idx] = { ...tasks[idx], status: msg.Status } as TaskItem;
              tasks = [...tasks];
            } else {
              // Unknown task (likely a new subtask) → fetch and append
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
        } catch {}
      };
      sse.onerror = () => {
        try { sse?.close(); } catch {}
        if (reconnectHandle) clearTimeout(reconnectHandle);
        reconnectHandle = setTimeout(() => {
          startSse();
        }, retryMs);
        retryMs = Math.min(retryMs * 2, maxRetryMs);
      };
    } catch {}
  }

  onMount(() => {
    startSse();
    return () => {
      try { sse?.close(); } catch {}
      if (reconnectHandle) clearTimeout(reconnectHandle);
    };
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
        <TaskDetails taskId={selectedId} />
      </div>
    </div>
  </div>
</main>
