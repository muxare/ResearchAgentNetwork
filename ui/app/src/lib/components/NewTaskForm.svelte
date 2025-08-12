<script lang="ts">
  import { showToast } from '$lib/stores/toast';
  import { createEventDispatcher } from 'svelte';
  const dispatch = createEventDispatcher<{ created: { id: string } }>();

  let description = $state('');
  let priority = $state(5);
  let saving = $state(false);

  async function submit() {
    const desc = description.trim();
    if (!desc) return;
    saving = true;
    try {
      const res = await fetch('/api/tasks', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ description: desc, priority })
      });
      if (res.ok) {
        const data = await res.json();
        showToast('Task created', 'success');
        dispatch('created', { id: data.id });
        description = '';
      } else {
        const msg = await res.text();
        showToast(msg || 'Failed to create task', 'error');
      }
    } catch (e: any) {
      showToast(e?.message || 'Failed to create task', 'error');
    } finally {
      saving = false;
    }
  }
</script>

<div class="p-3 rounded border bg-white">
  <h3 class="text-sm font-semibold mb-2">New Task</h3>
  <div class="flex flex-col gap-2">
    <label class="text-sm">
      <div class="text-xs text-gray-600">Description</div>
      <textarea class="border rounded px-2 py-1 w-full min-h-20" bind:value={description} placeholder="What should the agent research?"></textarea>
    </label>
    <label class="text-sm w-40">
      <div class="text-xs text-gray-600">Priority</div>
      <input class="border rounded px-2 py-1 w-full" type="number" min="1" max="10" bind:value={priority} />
    </label>
    <div>
      <button type="button" class="px-3 py-1.5 rounded border text-sm bg-green-50 hover:bg-green-100 disabled:opacity-50" disabled={saving} onclick={submit}>
        {saving ? 'Submitting…' : 'Submit Task'}
      </button>
    </div>
  </div>
</div>

