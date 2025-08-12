<script lang="ts">
  export interface TaskItem {
    id: string;
    description: string;
    status: string;
    createdAt?: string;
    priority?: number;
  }

  let { task, onselect }: { task: TaskItem & { flashUntil?: number }; onselect?: (detail: { id: string }) => void } = $props();
  let now = $state(Date.now());
  const tick = setInterval(() => { now = Date.now(); }, 250);
  $effect(() => () => clearInterval(tick));

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

<button type="button" class={`w-full text-left p-2 rounded-lg border bg-white hover:shadow-md transition group text-xs ${task.flashUntil && task.flashUntil > now ? 'ring-2 ring-offset-1 ring-yellow-300' : ''}`}
  onclick={() => onselect?.({ id: task.id })}>
  <div class="flex items-center justify-between">
    <span class="text-[10px] text-gray-500 font-mono truncate max-w-[96px]">{task.id}</span>
    <span class={`text-[10px] px-1 py-0.5 rounded-full shadow ${statusToColor[task.status] ?? 'bg-gray-400 text-white'}`}>{task.status}</span>
  </div>
  <div class="mt-1 text-xs font-medium leading-tight text-slate-800 line-clamp-2 group-hover:line-clamp-4">{task.description}</div>
  <div class="mt-1 flex items-center gap-2 text-[10px] text-gray-500">
    {#if typeof task.priority === 'number'}
      <span class="px-1.5 py-0.5 rounded bg-slate-100">prio {task.priority}</span>
    {/if}
    {#if task.createdAt}
      <span>{formatDate(task.createdAt)}</span>
    {/if}
  </div>
</button>

