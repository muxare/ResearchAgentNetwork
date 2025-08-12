<script lang="ts">
  const items = [
    { name: 'Pending', cls: 'bg-gray-500' },
    { name: 'Analyzing', cls: 'bg-blue-600' },
    { name: 'Executing', cls: 'bg-purple-600' },
    { name: 'Aggregating', cls: 'bg-orange-500' },
    { name: 'Completed', cls: 'bg-green-600' },
    { name: 'Failed', cls: 'bg-red-600' }
  ];
</script>

<div class="p-3 rounded border bg-white">
  <h3 class="text-sm font-semibold mb-2">Status Legend</h3>
  <div class="flex flex-wrap gap-4 text-sm">
    {#each items as it}
      <div class="inline-flex items-center gap-2">
        <span class={`w-3 h-3 rounded-full ${it.cls}`}></span>
        <span class="text-gray-700">{it.name}</span>
      </div>
    {/each}
  </div>
</div>

