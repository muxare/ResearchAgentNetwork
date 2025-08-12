<script lang="ts">
  let depth = $state<number | ''>('' as any);
  let logPrompts = $state(false);
  let msg = $state('');

  async function load() {
    try {
      const res = await fetch('/api/settings');
      if (res.ok) {
        const s = await res.json();
        depth = s.maxDecompositionDepth ?? '';
        logPrompts = !!s.logPrompts;
      }
    } catch {}
  }
  import { showToast } from '$lib/stores/toast';

  async function save() {
    try {
      const body: any = { maxDecompositionDepth: typeof depth === 'number' ? depth : undefined, logPrompts };
      const res = await fetch('/api/settings', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) });
      msg = res.ok ? 'Saved' : 'Failed';
      showToast(msg, res.ok ? 'success' : 'error');
    } catch {
      msg = 'Failed';
      showToast('Failed', 'error');
    }
    setTimeout(() => (msg = ''), 1500);
  }

  $effect(() => { load(); });
</script>

<div class="p-3 rounded border bg-white">
  <h3 class="text-sm font-semibold">Settings</h3>
  <div class="mt-2 flex items-end gap-3">
    <label class="text-sm">
      <div class="text-xs text-gray-600">Max Decomposition Depth</div>
      <input class="border rounded px-2 py-1" type="number" bind:value={depth} min="0" />
    </label>
    <label class="text-sm inline-flex items-center gap-2">
      <input type="checkbox" bind:checked={logPrompts} />
      <span>Log Prompts</span>
    </label>
    <button type="button" class="px-2 py-1 rounded border text-xs bg-gray-50 hover:bg-gray-100" onclick={save}>Apply</button>
    {#if msg}
      <span class="text-xs text-gray-500">{msg}</span>
    {/if}
  </div>
  <p class="text-xs text-gray-500 mt-2">Updates affect runtime without restart.</p>
</div>

