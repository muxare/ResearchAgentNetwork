<script lang="ts">
  import { serverEvents } from '$lib/stores/events';

  type ActivityItem = {
    ts: string;
    taskId: string;
    status?: string;
    eventType?: string;
    message?: string;
  };

  let { selectedTaskId, limit = 100 }: { selectedTaskId?: string | null; limit?: number } = $props();
  let items = $state<ActivityItem[]>([]);

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

  $effect(() => {
    const unsub = serverEvents.subscribe((msg: any) => {
      if (!msg) return;
      if (msg.type === 'task' && msg.TaskId) {
        const STATUS_NAMES = ['Pending','Analyzing','Executing','Aggregating','Completed','Failed'] as const;
        const toStatusName = (val: any) => typeof val === 'string' ? val : (typeof val === 'number' ? (STATUS_NAMES as any)[val] ?? 'Pending' : 'Pending');
        const row: ActivityItem = {
          ts: new Date().toLocaleTimeString(),
          taskId: String(msg.TaskId),
          status: toStatusName(msg.Status),
          eventType: msg.EventType,
          message: msg.Message
        };
        items = [row, ...items].slice(0, limit);
      }
    });
    return () => unsub();
  });

  function filtered() {
    // Always show all activities; ignore selectedTaskId for filtering
    return items;
  }
</script>

<div class="p-3 rounded border bg-white">
  <div class="flex items-center justify-between">
    <h3 class="text-sm font-semibold">Activity</h3>
    <div class="text-xs text-gray-500">{filtered().length}/{items.length}</div>
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
          <span class="shrink-0 w-24">{it.status}</span>
					<span class="shrink-0 w-56 text-gray-600">{getVerifyHint(it)}</span>
          <span class="truncate">{it.message}</span>
        </li>
      {/each}
    </ul>
  {/if}
</div>

