<script lang="ts">
  import TaskCard from './TaskCard.svelte';
  import type { TaskItem } from './TaskCard.svelte';
  import { dndzone } from 'svelte-dnd-action';

  let { title, tasks, loading, onselect, onreorder }: {
    title: string;
    tasks: TaskItem[];
    loading?: boolean;
    onselect?: (detail: { id: string }) => void;
    onreorder?: (detail: { status: string; ids: string[] }) => void;
  } = $props();

  function onFinalize(e: CustomEvent<{ items: TaskItem[] }>) {
    const ids = (e.detail.items as any[]).map((it: any) => it.id as string);
    onreorder?.({ status: title, ids });
  }
</script>

<section class="flex flex-col gap-2 bg-white/80 rounded-xl p-3 border shadow-sm
  w-[48%] sm:w-[48%] md:w-[31%] lg:w-[23%] xl:w-[16%] min-w-[220px]">
  <header class="px-2 py-1 sticky top-0 z-10 bg-white/70 backdrop-blur border-b rounded-t-xl flex items-center gap-2">
    <span class={
      title === 'Pending' ? 'w-2 h-2 rounded-full bg-gray-500' :
      title === 'Analyzing' ? 'w-2 h-2 rounded-full bg-blue-600' :
      title === 'Executing' ? 'w-2 h-2 rounded-full bg-purple-600' :
      title === 'Aggregating' ? 'w-2 h-2 rounded-full bg-orange-500' :
      title === 'Completed' ? 'w-2 h-2 rounded-full bg-green-600' :
      'w-2 h-2 rounded-full bg-red-600'
    }></span>
    <h2 class="text-sm font-semibold">{title} <span class="text-xs text-gray-500">({tasks.length})</span></h2>
  </header>
  <div class="flex flex-col gap-2" use:dndzone={{ items: tasks, flipDurationMs: 120 }} onfinalize={onFinalize}>
    {#if loading}
      {#each Array(3) as _, i}
        <div class="p-3 rounded border bg-white animate-pulse">
          <div class="h-3 bg-gray-200 rounded w-1/2"></div>
          <div class="h-4 bg-gray-200 rounded w-5/6 mt-2"></div>
          <div class="h-3 bg-gray-200 rounded w-1/3 mt-2"></div>
        </div>
      {/each}
    {:else if tasks.length === 0}
      <div class="text-xs text-gray-500 p-2">No tasks in {title}</div>
    {:else}
      {#each tasks as t (t.id)}
        <TaskCard task={t} onselect={(detail) => onselect?.(detail)} />
      {/each}
    {/if}
  </div>
</section>

