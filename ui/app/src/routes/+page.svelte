<script lang="ts">
  // use onMount to avoid re-running initialization on reactive updates
  import KanbanBoard from '$lib/components/KanbanBoard.svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  import StatusBar from '$lib/components/StatusBar.svelte';
  import ErrorBanner from '$lib/components/ErrorBanner.svelte';
  import TaskDetailsModal from '$lib/components/TaskDetailsModal.svelte';
  import SettingsPanel from '$lib/components/SettingsPanel.svelte';
  import NewTaskForm from '$lib/components/NewTaskForm.svelte';
  import ActivityPanel from '$lib/components/ActivityPanel.svelte';
  import Card from '$lib/components/Card.svelte';
  import type { TaskItem } from '$lib/types/tasks';
  import { tasks as tasksStore, loading as loadingStore, error as errorStore, connection as connectionStore, reconnectInMs as reconnectStore, refreshTasks, setInitialTasks, start, stop } from '$lib/stores/tasks';
  import { onMount } from 'svelte';
  let { data }: { data: { tasks: TaskItem[] } } = $props();
  let filterText = $state('');
  let selectedId = $state<string | null>(null);
  let showDetails = $state(false);
  
  // Initialize tasks store with server-provided data and start background sync
  onMount(() => {
    setInitialTasks(data?.tasks ?? []);
    start();
    return () => stop();
  });

  function filtered(list: TaskItem[]) {
    const q = filterText.toLowerCase();
    return list.filter(t => (!q || t.description.toLowerCase().includes(q) || (t.id ?? '').toLowerCase().includes(q)));
  }

  // Stores to values
  const tasks = tasksStore;
  const loading = loadingStore;
  const error = errorStore;
  const connection = connectionStore;
  const reconnectInMs = reconnectStore;
</script>

<main class="p-6 md:p-8 min-h-screen">
  <div class="flex items-baseline justify-between">
    <PageHeader title="RAN UI" subtitle="SvelteKit + Tailwind — Kanban" />
  </div>

  <div class="mt-4 flex flex-col gap-4">
    <Card padded>
      <StatusBar connection={$connection} reconnectInMs={$reconnectInMs} tasks={$tasks} />
    </Card>
    
    {#if $error}
      <ErrorBanner message={$error} />
    {/if}

    <SettingsPanel />
    
    <NewTaskForm on:created={() => { refreshTasks(); }} />

    <div class="space-y-6">
      <Card>
        <KanbanBoard tasks={filtered($tasks)} loading={$loading} onselect={({ id }: { id: string }) => { selectedId = id; showDetails = true; }} />
      </Card>
      
      <TaskDetailsModal
        open={showDetails}
        taskId={selectedId}
        on:close={() => (showDetails = false)}
        on:select={(e: CustomEvent<{ id: string }>) => (selectedId = e.detail.id)}
      />
      <Card padded>
        <ActivityPanel selectedTaskId={selectedId} limit={300} />
      </Card>
    </div>
  </div>
</main>
