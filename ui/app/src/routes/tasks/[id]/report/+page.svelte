<script lang="ts">
  import { onMount } from 'svelte';
  import PageHeader from '$lib/components/PageHeader.svelte';
  let { params }: { params: { id: string } } = $props();
  let loading = $state(true);
  let error = $state<string>('');
  let markdown = $state<string>('');
  let html = $state<string>('');

  async function loadReport() {
    loading = true;
    error = '';
    try {
      const res = await fetch(`/api/reports/${params.id}`);
      if (!res.ok) throw new Error(await res.text());
      const data = await res.json();
      markdown = String(data.markdown ?? '');
    } catch (e: any) {
      error = e?.message || 'Failed to load report';
    } finally {
      loading = false;
    }
  }

  function download() {
    window.location.href = `/api/reports/${params.id}/download?format=md`;
  }

  // parse markdown using marked on idle
  let ticket = 0;
  async function scheduleParse() {
    const t = ++ticket;
    try {
      const mod: any = await import('marked');
      const m = mod?.marked ?? mod?.default ?? mod;
      const out = m?.parse ? m.parse(markdown) : String(markdown ?? '');
      const htmlStr = typeof out === 'string' ? out : await out;
      if (t === ticket) html = htmlStr;
    } catch {
      if (t === ticket) html = String(markdown ?? '');
    }
  }

  $effect(() => {
    const m = markdown;
    // @ts-ignore: requestIdleCallback may exist
    const ric = (window as any)?.requestIdleCallback as ((cb: () => void) => number) | undefined;
    if (ric) ric(() => void scheduleParse()); else setTimeout(() => void scheduleParse(), 0);
  });

  onMount(() => { loadReport(); });
</script>

<main class="p-6 md:p-8 min-h-screen">
  <PageHeader title="Task Report" subtitle={`Task ${params.id}`} />

  {#if loading}
    <div class="mt-4 p-4 border rounded bg-white animate-pulse">
      Loading report...
    </div>
  {:else if error}
    <div class="mt-4 p-3 border rounded bg-red-50 text-red-700 text-sm">{error}</div>
  {:else}
    <div class="mt-4 flex items-center gap-2">
      <button class="px-3 py-1.5 text-xs rounded bg-slate-800 text-white hover:bg-slate-900" on:click={download}>Download Markdown</button>
      <button class="px-3 py-1.5 text-xs rounded bg-slate-100 hover:bg-slate-200" on:click={loadReport}>Refresh</button>
    </div>
    <article class="mt-4 prose prose-sm max-w-none">{@html html}</article>
  {/if}
</main>

