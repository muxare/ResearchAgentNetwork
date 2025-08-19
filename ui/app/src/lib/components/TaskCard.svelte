<script lang="ts">
  export interface TaskItem {
    id: string;
    description: string;
    status: string;
    createdAt?: string;
    priority?: number;
    parentTaskId?: string;
    depth?: number;
    isRoot?: boolean;
    isSystemTask?: boolean;
    category?: string;
  }

  import { now as nowStore } from '$lib/stores/now';
  let { task, onselect }: { task: TaskItem & { flashUntil?: number }; onselect?: (detail: { id: string }) => void } = $props();
  // Use shared timer to avoid per-card intervals
  const now = nowStore;

  const statusToColor: Record<string, string> = {
    Pending: 'bg-gray-500 text-white',
    Analyzing: 'bg-blue-600 text-white',
    Executing: 'bg-purple-600 text-white',
    Aggregating: 'bg-orange-500 text-white',
    Completed: 'bg-green-600 text-white',
    Failed: 'bg-red-600 text-white'
  };

  function formatDate(iso?: string) {
    if (!iso) return '';
    try {
      return new Date(iso).toLocaleString();
    } catch {
      return iso;
    }
  }
</script>

<button type="button" class={`relative w-full text-left p-2 rounded-lg border bg-white hover:shadow-md transition group text-xs ${task.flashUntil && task.flashUntil > $now ? 'ring-2 ring-offset-1 ring-yellow-300' : ''}`}
on:click={() => onselect?.({ id: task.id })}>
  {#if (task as any)._stackCount > 0}
    <span class="pointer-events-none absolute inset-0 -z-10">
      <span class="absolute inset-0 translate-x-1 translate-y-1 rounded-lg border bg-white/90 shadow-sm"></span>
      <span class="absolute inset-0 translate-x-2 translate-y-2 rounded-lg border bg-white/80 shadow-sm"></span>
    </span>
  {/if}
  <div class="flex items-center justify-between">
    <span class="text-[10px] text-gray-500 font-mono truncate max-w-[96px]">{task.id}</span>
    <span class={`text-[10px] px-1 py-0.5 rounded-full shadow ${statusToColor[task.status] ?? 'bg-gray-400 text-white'}`}>
      {task.status}
      {#if (task as any)._stackCount > 0}
        <span class="ml-1 text-[9px] text-gray-700 align-middle">(+{(task as any)._stackCount})</span>
      {/if}
    </span>
  </div>
  <div class="mt-1 text-xs font-medium leading-tight text-slate-800 line-clamp-2 group-hover:line-clamp-4">
    {#if task.isRoot}
      <span class="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200">root</span>
    {:else if typeof task.depth === 'number' && task.depth > 0}
      <span class="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-slate-50 text-slate-700 border border-slate-200">lvl {task.depth}</span>
    {/if}
    {#if task.isSystemTask}
      <span class="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-indigo-50 text-indigo-700 border border-indigo-200">system</span>
    {/if}
    {#if (task.category ?? '').toLowerCase() === 'finalization'}
      <span class="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-cyan-50 text-cyan-700 border border-cyan-200">finalization</span>
    {/if}
    {task.description}
  </div>
  <div class="mt-1 flex items-center gap-2 text-[10px] text-gray-500">
    {#if typeof task.priority === 'number'}
      <span class="px-1.5 py-0.5 rounded bg-slate-100">prio {task.priority}</span>
    {/if}
    {#if task.createdAt}
      <span>{formatDate(task.createdAt)}</span>
    {/if}
  </div>
  {#if task.status === 'Analyzing' || task.status === 'Executing' || task.status === 'Aggregating'}
    <div class="mt-2 flex items-center gap-2 text-[10px]">
      <span class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-yellow-50 text-yellow-700 border border-yellow-200">
        <span class="relative flex h-2 w-2">
          <span class="animate-ping absolute inline-flex h-full w-full rounded-full bg-yellow-400 opacity-75"></span>
          <span class="relative inline-flex rounded-full h-2 w-2 bg-yellow-500"></span>
        </span>
        LLM active
      </span>
    </div>
  {/if}
</button>

