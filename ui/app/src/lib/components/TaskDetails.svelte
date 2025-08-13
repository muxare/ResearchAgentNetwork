<script lang="ts">
  import { onMount } from 'svelte';
  import { marked } from 'marked';
  import { serverEvents } from '$lib/stores/events';
  import { createEventDispatcher } from 'svelte';
  const dispatch = createEventDispatcher<{ select: { id: string } }>();

  export interface TaskDetailsData {
    id: string;
    description?: string;
    status?: string;
    priority?: number;
  }

  let { taskId }: { taskId: string | null } = $props();
  let loading = $state(false);
  let raw = $state('');
  let html = $state('');
  let meta = $state<TaskDetailsData | null>(null);
  let err = $state('');
  let live = $state<string[]>([]);
  // centralized SSE via serverEvents store
  
  import { showToast } from '$lib/stores/toast';

  async function action(kind: 'retry' | 'cancel' | 'force') {
    if (!taskId) return;
    try {
      const res = await fetch(`/api/tasks/${taskId}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: kind }) });
      showToast(res.ok ? `${kind} sent` : `${kind} failed`, res.ok ? 'success' : 'error');
    } catch { showToast(`${kind} failed`, 'error'); }
  }

  const STATUS_NAMES = ['Pending','Analyzing','Executing','Aggregating','Completed','Failed'] as const;
  function toStatusName(val: any): string {
    if (typeof val === 'string') return val;
    if (typeof val === 'number') return STATUS_NAMES[val] ?? 'Pending';
    return 'Pending';
  }

  async function load() {
    if (!taskId) return;
    loading = true; err = '';
    try {
      const [taskRes, repRes, childrenRes] = await Promise.all([
        fetch(`/api/tasks/${taskId}`),
        fetch(`/api/tasks/${taskId}/report`),
        fetch(`/api/tasks/${taskId}/children`)
      ]);
      if (taskRes.ok) {
        const raw = await taskRes.json();
        meta = { ...raw, status: toStatusName(raw?.status) };
      }
      const md = repRes.ok ? await repRes.text() : 'No report available';
      raw = md;
      html = await (marked.parse(md) as Promise<string> | string);
      if (childrenRes.ok) {
        const list = await childrenRes.json();
        children = Array.isArray(list) ? list : [];
      } else {
        children = [];
      }
    } catch (e:any) {
      err = e.message || 'Failed to load details';
    } finally {
      loading = false;
    }
  }

  $effect(() => { load(); });

  // Live events stream for selected task via shared store
  $effect(() => {
    const unsub = serverEvents.subscribe((msg: any) => {
      if (!taskId || !msg) return;
      if (msg.type === 'task' && msg.TaskId && (msg.TaskId + '').toLowerCase() === (taskId + '').toLowerCase()) {
        const line = `${new Date().toLocaleTimeString()} ${msg.EventType} → ${msg.Status}${msg.Message ? ' — ' + msg.Message : ''}`;
        live = [...live.slice(-199), line];
      }
    });
    return () => unsub();
  });
  type ChildTask = { id: string; description: string; status: string };
  let children = $state<ChildTask[]>([]);

  function open(id: string) {
    dispatch('select', { id });
  }
</script>

{#if !taskId}
  <div class="text-sm text-gray-500">Select a task…</div>
{:else if loading}
  <div class="text-sm text-gray-500">Loading…</div>
{:else}
  {#if err}
    <div class="text-sm text-red-600">{err}</div>
  {/if}
  {#if meta}
    <div class="mb-2">
      <div class="font-semibold">{meta.description}</div>
      <div class="text-xs text-gray-500">{meta.status} • prio {meta.priority}</div>
      <div class="text-xs text-gray-400">{meta.id}</div>
    </div>
    <div class="mb-4 text-xs text-gray-600 flex flex-wrap gap-2">
      {#if meta && (meta as any).parentTaskId}
        <button type="button" class="px-2 py-0.5 rounded border bg-gray-50 hover:bg-gray-100" onclick={() => open((meta as any).parentTaskId)}>↑ Parent</button>
      {/if}
      {#if children.length > 0}
        <span class="text-gray-500">Children:</span>
        {#each children as c}
          <button type="button" class="px-2 py-0.5 rounded border bg-gray-50 hover:bg-gray-100" onclick={() => open(c.id)}>{c.status}: {c.description}</button>
        {/each}
      {:else}
        <span class="text-gray-400">No children</span>
      {/if}
    </div>
    <div class="flex gap-2 mb-4">
      <button type="button" class="px-2 py-1 rounded border text-xs bg-blue-50 hover:bg-blue-100" onclick={() => action('retry')}>Retry</button>
      <button type="button" class="px-2 py-1 rounded border text-xs bg-orange-50 hover:bg-orange-100" onclick={() => action('cancel')}>Cancel</button>
      <button type="button" class="px-2 py-1 rounded border text-xs bg-purple-50 hover:bg-purple-100" onclick={() => action('force')}>Force Execute</button>
    </div>
  {/if}
  <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
    <div>
      <h3 class="text-sm font-semibold">Report (rendered)</h3>
      <div class="prose max-w-none mt-2">{@html html}</div>
    </div>
    <div>
      <h3 class="text-sm font-semibold">Report (raw)</h3>
      <pre class="text-xs text-gray-600 mt-2 whitespace-pre-wrap">{raw}</pre>
    </div>
  </div>
  <div class="mt-4">
    <h3 class="text-sm font-semibold">Live Events</h3>
    <pre class="text-xs text-gray-600 mt-2 whitespace-pre-wrap max-h-64 overflow-auto">{live.join('\n')}</pre>
  </div>
{/if}

