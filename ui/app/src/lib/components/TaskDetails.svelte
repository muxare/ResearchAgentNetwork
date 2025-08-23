<script lang="ts">
  // @ts-nocheck
  // lazy parse markdown when needed to avoid blocking UI on selection
  import { serverEvents } from '$lib/stores/events';
  import { createEventDispatcher } from 'svelte';
  const dispatch = createEventDispatcher<{ select: { id: string } }>();
  import { toStatusName } from '$lib/utils/status';
  import { base } from '$app/paths';

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
  let tab = $state<'overview' | 'report' | 'raw' | 'events' | 'timeline'>('overview');
  type TimelineItem = { timestampUtc: string; eventType: string; status: string; message?: string };
  let timeline = $state<TimelineItem[]>([]);
  let timelineErr = $state('');
  let includeChildren = $state(false);

  async function action(kind: 'retry' | 'cancel' | 'force') {
    if (!taskId) return;
    try {
      const res = await fetch(`/api/tasks/${taskId}`, { method: 'PATCH', headers: { 'Content-Type':'application/json' }, body: JSON.stringify({ action: kind }) });
      showToast(res.ok ? `${kind} sent` : `${kind} failed`, res.ok ? 'success' : 'error');
    } catch { showToast(`${kind} failed`, 'error'); }
  }


  let currentAbort: AbortController | null = null;
  async function load() {
    if (!taskId) return;
    loading = true; err = '';
    html = '';
    try {
      currentAbort?.abort();
      currentAbort = new AbortController();
      const signal = currentAbort.signal;
      const [taskRes, repRes, childrenRes, eventsRes] = await Promise.all([
        fetch(`/api/tasks/${taskId}` , { signal }),
        fetch(`/api/tasks/${taskId}/report`, { signal }),
        fetch(`/api/tasks/${taskId}/children`, { signal }),
        fetch(`/api/tasks/${taskId}/events?includeChildren=${includeChildren ? 'true' : 'false'}`, { signal })
      ]);
      if (taskRes.ok) {
        const raw = await taskRes.json();
        meta = { ...raw, status: toStatusName(raw?.status) };
      }
      const md = repRes.ok ? await repRes.text() : 'No report available';
      raw = md;
      // defer parsing until Report tab is visible
      if (tab === 'report') scheduleParse();
      if (childrenRes.ok) {
        const list = await childrenRes.json();
        children = Array.isArray(list) ? list : [];
      } else {
        children = [];
      }
      if (eventsRes.ok) {
        const evs = await eventsRes.json();
        timeline = Array.isArray(evs) ? evs.map((e:any) => ({ timestampUtc: e.timestampUtc, eventType: e.eventType, status: toStatusName(e.status), message: e.detailsJson ? `json:${e.detailsJson}` : e.message })) : [];
        timelineErr = '';
      } else {
        timeline = [];
        timelineErr = 'Failed to load timeline';
      }
    } catch (e:any) {
      err = e.message || 'Failed to load details';
    } finally {
      loading = false;
    }
  }

  // Only reload when taskId changes; guard against re-running due to inner state
  let lastLoadedId: string | null = null;
  $effect(() => {
    const id = taskId;
    if (id && id !== lastLoadedId) {
      lastLoadedId = id;
      load();
    }
  });

  // Live events stream for selected task via shared store
  import { onMount } from 'svelte';
  onMount(() => {
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

  // Defer markdown parsing to idle time and only when Report is active
  let parseTicket = 0;
  function scheduleParse() {
    const ticket = ++parseTicket;
    const run = async () => {
      try {
        const mod: any = await import('marked');
        const m = mod?.marked ?? mod?.default ?? mod;
        const out = m?.parse ? m.parse(raw) : String(raw ?? '');
        const htmlStr = typeof out === 'string' ? out : await out;
        if (ticket === parseTicket) html = htmlStr;
      } catch {
        if (ticket === parseTicket) html = String(raw ?? '');
      }
    };
    try {
      // @ts-ignore requestIdleCallback may exist in browsers
      const ric = (window as any).requestIdleCallback as ((cb: () => void) => number) | undefined;
      if (ric) ric(() => void run()); else setTimeout(() => void run(), 0);
    } catch {
      setTimeout(() => void run(), 0);
    }
  }

  // Re-parse when switching to Report tab or when raw changes while on Report
  $effect(() => {
    const t = tab;
    const r = raw;
    if (t === 'report') scheduleParse();
  });

  function open(id: string) {
    dispatch('select', { id });
  }

  function parseMaybeJson(x?: string): any | null {
    if (!x) return null;
    const s = String(x);
    if (s.startsWith('json:')) {
      try { return JSON.parse(s.substring(5)); } catch { return null; }
    }
    return null;
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
    <div class="mb-3 border-b">
      <nav class="flex gap-2 text-xs">
        <button type="button" class={`px-2 py-1 ${tab==='overview' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onclick={() => (tab='overview')}>Overview</button>
        <button type="button" class={`px-2 py-1 ${tab==='report' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onclick={() => (tab='report')}>Report</button>
        <button type="button" class={`px-2 py-1 ${tab==='raw' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onclick={() => (tab='raw')}>Raw</button>
        <button type="button" class={`px-2 py-1 ${tab==='events' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onclick={() => (tab='events')}>Events</button>
        <button type="button" class={`px-2 py-1 ${tab==='timeline' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onclick={() => (tab='timeline')}>Timeline</button>
      </nav>
    </div>
    {#if tab === 'overview'}
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
    {:else if tab === 'report'}
      <div>
        <h3 class="text-sm font-semibold">Report (rendered)</h3>
        <div class="flex items-center gap-2 mt-2">
          <a class="text-xs px-2 py-1 rounded bg-slate-100 hover:bg-slate-200" href={`/api/reports/${meta.id}/download?format=md`} target="_blank" rel="noreferrer">Download MD</a>
          <a class="text-xs px-2 py-1 rounded bg-blue-600 text-white hover:bg-blue-700" data-sveltekit-preload-data="hover" href={`${base}/tasks/${meta.id}/report`}>Open In-App Viewer</a>
        </div>
        <div class="prose max-w-none mt-2">{@html html}</div>
      </div>
    {:else if tab === 'raw'}
      <div>
        <h3 class="text-sm font-semibold">Report (raw)</h3>
        <pre class="text-xs text-gray-600 mt-2 whitespace-pre-wrap">{raw}</pre>
      </div>
    {:else if tab === 'events'}
      <div>
        <h3 class="text-sm font-semibold">Live Events</h3>
        <pre class="text-xs text-gray-600 mt-2 whitespace-pre-wrap max-h-64 overflow-auto">{live.join('\n')}</pre>
      </div>
    {:else if tab === 'timeline'}
      <div>
        <div class="flex items-center justify-between">
          <h3 class="text-sm font-semibold">Timeline</h3>
          <div class="flex items-center gap-3">
            <a class="text-xs text-blue-700 hover:underline" target="_blank" href={`/api/tasks/${taskId}/report`}>open report</a>
            <label class="text-xs text-gray-600 flex items-center gap-1">
              <input type="checkbox" bind:checked={includeChildren} onchange={() => load()} /> include children
            </label>
          </div>
        </div>
        {#if timelineErr}
          <div class="text-xs text-red-600 mt-2">{timelineErr}</div>
        {:else if timeline.length === 0}
          <div class="text-xs text-gray-500 mt-2">No events.</div>
        {:else}
          <ul class="mt-2 space-y-1 max-h-64 overflow-auto text-xs text-gray-700">
            {#each timeline as it}
              <li class="flex items-start gap-2">
                <span class="text-gray-500 shrink-0 w-40">{new Date(it.timestampUtc).toLocaleString()}</span>
                <span class="shrink-0 w-28">{it.eventType}</span>
                <span class="shrink-0 w-24">{it.status}</span>
                {#if parseMaybeJson(it.message)}
                  <pre class="truncate max-w-[28rem] text-[10px] text-gray-600">{JSON.stringify(parseMaybeJson(it.message), null, 2)}</pre>
                {:else}
                  <span class="truncate">{it.message}</span>
                {/if}
              </li>
            {/each}
          </ul>
        {/if}
      </div>
    {/if}
  {/if}
{/if}

