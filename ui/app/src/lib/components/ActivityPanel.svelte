<script lang="ts">
  // @ts-nocheck
  import { serverEvents } from '$lib/stores/events';

  type ActivityItem = {
    ts: string;
    tsMs: number;
    taskId: string;
    status?: string;
    eventType?: string;
    message?: string;
    agent?: string;
  };

  let { selectedTaskId, limit }: { selectedTaskId?: string | null; limit?: number } = $props();
  let items = $state<ActivityItem[]>([]);
  let filterSubtree = $state(false);
  let filterAgent = $state<string>('');
  let filterType = $state<string>('');
  let filterSinceMin = $state<number>(0);

  function isInSubtree(it: ActivityItem): boolean {
    if (!selectedTaskId) return true;
    if (!filterSubtree) return true;
    // Fast path: direct task match
    if ((it.taskId || '').toLowerCase() === (selectedTaskId || '').toLowerCase()) return true;
    // Heuristic: treat events with ParentTaskId in message (if present) as descendants
    try {
      const msg: string = it.message || '';
      if (msg.startsWith('json:')) {
        const obj = JSON.parse(msg.substring(5));
        const pid = (obj.parentTaskId || obj.parent || obj.parent_id || '').toString();
        if (pid && pid.toLowerCase() === (selectedTaskId || '').toLowerCase()) return true;
      }
    } catch {}
    return true; // default allow to avoid hiding events until full tree available
  }

	function getVerifyHint(it: ActivityItem): string {
		const type = (it.eventType || '').toLowerCase();
		const status = (it.status || '').toLowerCase();
		switch (type) {
			case 'submitted':
				return 'Open Task Details to track status.';
			case 'status':
				if (status === 'analyzing') return 'Watch for decomposition or direct execution.';
				if (status === 'executing') return 'Wait for completed; then open report.';
				if (status === 'aggregating') return 'Open parent task; synthesized report incoming.';
				return '';
			case 'decomposed':
				return 'Open parent Task Details; verify subtasks listed.';
			case 'retrieved':
				return 'See retrieved count in message; expect richer report.';
			case 'ingested':
				return 'Web results ingested; sources may appear in report.';
			case 'completed':
				return 'Open Task Details → Report and Sources.';
			case 'failed':
				return 'Open Task Details → consider Retry/Force Execute.';
			case 'aggregated':
				return 'Open parent Task Details → synthesized report.';
			case 'stored':
				return 'Stored in memory; future similar tasks may reuse.';
			case 'skipped':
				return 'No storage; review report manually.';
			case 'refined':
				return 'Report updated; refresh Task Details.';
			case 'retry':
				return 'Re-queued; watch for status updates.';
			case 'merged':
				return "Check target task description for '(merged similar request)'.";
			default:
				return '';
		}
	}

  import { toStatusName } from '$lib/utils/status';

  import { onMount } from 'svelte';
  onMount(() => {
    const unsub = serverEvents.subscribe((msg: any) => {
      if (!msg) return;
      if (msg.type === 'task' && msg.TaskId) {
        const row: ActivityItem = {
          ts: new Date().toLocaleTimeString(),
          tsMs: Date.now(),
          taskId: String(msg.TaskId),
          status: toStatusName(msg.Status),
          eventType: msg.EventType,
          message: msg.Details ? `json:${JSON.stringify(msg.Details)}` : msg.Message,
          agent: msg.Agent || (msg.Details && msg.Details.agent ? msg.Details.agent : undefined)
        };
        items = (limit && isFinite(limit)) ? [row, ...items].slice(0, limit) : [row, ...items];
      }
    });
    return () => unsub();
  });

  function filtered() {
    const now = Date.now();
    return items
      .filter(isInSubtree)
      .filter(it => !filterAgent || (it.agent || '').toLowerCase() === filterAgent.toLowerCase())
      .filter(it => !filterType || (it.eventType || '').toLowerCase() === filterType.toLowerCase())
      .filter(it => {
        if (!filterSinceMin || filterSinceMin <= 0) return true;
        return (now - (it.tsMs || now)) <= filterSinceMin * 60_000;
      });
  }
</script>

<div class="p-3 rounded border bg-white">
  <div class="flex items-center justify-between">
    <h3 class="text-sm font-semibold">Activity</h3>
    <div class="flex items-center gap-3">
      <label class="flex items-center gap-1 text-xs text-gray-600">
        <input type="checkbox" bind:checked={filterSubtree} />
        subtree
      </label>
      <label class="flex items-center gap-1 text-xs text-gray-600">
        agent
        <input class="border rounded px-1 py-0.5 w-24" placeholder="any" bind:value={filterAgent} />
      </label>
      <label class="flex items-center gap-1 text-xs text-gray-600">
        type
        <input class="border rounded px-1 py-0.5 w-28" placeholder="any" bind:value={filterType} />
      </label>
      <label class="flex items-center gap-1 text-xs text-gray-600">
        since
        <select class="border rounded px-1 py-0.5" bind:value={filterSinceMin}>
          <option value={0}>all</option>
          <option value={10}>10m</option>
          <option value={30}>30m</option>
          <option value={60}>60m</option>
        </select>
      </label>
      <div class="text-xs text-gray-500">{filtered().length}/{items.length}</div>
    </div>
  </div>
  {#if items.length === 0}
    <div class="text-xs text-gray-500 mt-2">No recent activity.</div>
  {:else}
		<ul class="mt-2 space-y-1 max-h-64 overflow-auto text-xs text-gray-700">
      {#each filtered() as it}
        <li class="flex items-start gap-2">
          <span class="text-gray-500 shrink-0 w-20">{it.ts}</span>
          <span class="font-mono text-[10px] text-gray-500 shrink-0 w-36 truncate">{it.taskId}</span>
          <span class="shrink-0 w-20">{it.eventType}</span>
          <span class="shrink-0 w-24 text-gray-500">{it.agent}</span>
          <span class="shrink-0 w-24">{it.status}</span>
					<span class="shrink-0 w-56 text-gray-600">{getVerifyHint(it)}</span>
          <span class="truncate">{it.message}</span>
        </li>
      {/each}
    </ul>
  {/if}
</div>

