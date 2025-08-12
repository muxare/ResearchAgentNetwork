<script lang="ts">
  import { toasts, removeToast, type Toast } from '$lib/stores/toast';
  let list = $state<Toast[]>([]);
  const unsub = toasts.subscribe((v) => (list = v));
  $effect(() => () => unsub());
</script>

<div class="fixed bottom-4 right-4 flex flex-col gap-2 z-50">
  {#each list as t (t.id)}
    <button type="button" class={
      'px-3 py-2 rounded shadow text-sm text-white text-left ' +
      (t.type === 'success' ? 'bg-green-600' : t.type === 'error' ? 'bg-red-600' : 'bg-gray-700')
    } onclick={() => removeToast(t.id)}>
      {t.message}
    </button>
  {/each}
  {#if list.length === 0}
    <!-- keep mount point -->
  {/if}
  
</div>

