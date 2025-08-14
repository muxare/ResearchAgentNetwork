<script lang="ts">
  import TaskDetails from '$lib/components/TaskDetails.svelte';
  import { createEventDispatcher } from 'svelte';

  export let open: boolean = false;
  export let taskId: string | null = null;

  const dispatch = createEventDispatcher();
</script>

{#if open}
  <div class="fixed inset-0 z-40 flex items-start md:items-center justify-center p-2 md:p-6">
    <button type="button" class="absolute inset-0 bg-black/30" aria-label="Close" on:click={() => dispatch('close')}></button>
    <div class="relative z-10 w-full md:w-5/6 lg:w-3/4 xl:w-2/3 max-h-[85vh] overflow-auto bg-white rounded-xl shadow-xl border p-3">
      <div class="flex items-center justify-between border-b pb-2 mb-3">
        <h2 class="text-sm font-semibold">Task details</h2>
        <button type="button" class="px-2 py-1 rounded border text-xs bg-gray-50 hover:bg-gray-100" on:click={() => dispatch('close')}>Close</button>
      </div>
      <TaskDetails taskId={taskId} on:select={(e: CustomEvent<{ id: string }>) => dispatch('select', e.detail)} />
    </div>
  </div>
{/if}

