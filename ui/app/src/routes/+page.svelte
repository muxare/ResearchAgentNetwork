<script lang="ts">
  /// <reference types="svelte" />
  /// <reference types="svelte/elements" />
  // use $effect instead of onMount for Svelte 5 runes
  import KanbanBoard from '$lib/components/KanbanBoard.svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  import StatusBar from '$lib/components/StatusBar.svelte';
  import ErrorBanner from '$lib/components/ErrorBanner.svelte';
  import TaskDetailsModal from '$lib/components/TaskDetailsModal.svelte';
  import SettingsPanel from '$lib/components/SettingsPanel.svelte';
  import NewTaskForm from '$lib/components/NewTaskForm.svelte';
  import { serverEvents } from '$lib/stores/events';
  import ActivityPanel from '$lib/components/ActivityPanel.svelte';
  import type { TaskItem } from '$lib/types/tasks';
  import { STATUS_NAMES } from '$lib/constants/statuses';
  let tasks = $state<TaskItem[]>([]);
  let filterText = $state('');
  let selectedId = $state<string | null>(null);
  let showDetails = $state(false);
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
  $effect(() => { refreshTasks(); });

  function filtered(list: TaskItem[]) {
    const q = filterText.toLowerCase();
    return list.filter(t => (!q || t.description.toLowerCase().includes(q) || (t.id ?? '').toLowerCase().includes(q)));
  }

  // Subscribe to centralized server events
  $effect(() => {
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
  $effect(() => {
    const h = setInterval(() => { refreshTasks(); }, 20000);
    return () => clearInterval(h);
  });
</script>

<main class="p-6 md:p-8 min-h-screen">
  <div class="flex items-baseline justify-between">
    <PageHeader title="RAN UI" subtitle="SvelteKit + Tailwind — Kanban" />
  </div>

  <div class="mt-4 flex flex-col gap-4">
    <StatusBar {connection} {reconnectInMs} {tasks} />
    
    {#if error}
      <ErrorBanner message={error} />
    {/if}

    <SettingsPanel />
    
    <NewTaskForm on:created={() => { refreshTasks(); }} />

    <div class="space-y-6">
      <div class="bg-white/80 border rounded-xl shadow-sm">
        <KanbanBoard tasks={filtered(tasks)} loading={loading} onselect={({ id }: { id: string }) => { selectedId = id; showDetails = true; }} />
      </div>
      
      <TaskDetailsModal
        open={showDetails}
        taskId={selectedId}
        on:close={() => (showDetails = false)}
        on:select={(e: CustomEvent<{ id: string }>) => (selectedId = e.detail.id)}
      />
      <div class="bg-white/80 border rounded-xl p-3 shadow-sm">
        <ActivityPanel selectedTaskId={selectedId} />
      </div>
    </div>
  </div>
</main>
