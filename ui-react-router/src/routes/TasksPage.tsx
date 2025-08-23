import React from 'react';
import { TaskCard, TaskItem } from '../components/TaskCard';
import { TaskDetailsModal } from '../components/TaskDetailsModal';
import { connectServerEvents } from '../lib/events';

type ProgressSummary = { total: number; pending: number; analyzing: number; executing: number; aggregating: number; completed: number; failed: number };

async function fetchJson<T>(url: string, signal?: AbortSignal): Promise<T> {
	const res = await fetch(url, { signal });
	if (!res.ok) throw new Error(await res.text());
	return res.json();
}

function isAbortError(err: any): boolean {
	const name = err?.name ?? '';
	const msg = typeof err === 'string' ? err : (err?.message ?? '');
	const str = String(err ?? '');
	return name === 'AbortError' || /aborted/i.test(msg) || /AbortError/i.test(str);
}

export const TasksPage: React.FC = () => {
	const [tasks, setTasks] = React.useState<TaskItem[]>([]);
	const [selectedId, setSelectedId] = React.useState<string | null>(null);
	const [loading, setLoading] = React.useState<boolean>(true);
	const [error, setError] = React.useState<string>('');
	const [summary, setSummary] = React.useState<ProgressSummary | null>(null);

	React.useEffect(() => {
		let alive = true;
		const ac = new AbortController();
		(async () => {
			try {
				setLoading(true);
				setError('');
				const [list, prog] = await Promise.all([
					fetchJson<any[]>('/api/tasks', ac.signal),
					fetchJson<ProgressSummary>('/api/progress', ac.signal)
				]);
				if (!alive) return;
				const normalized: TaskItem[] = (list ?? []).map((t: any) => ({
					id: t.id,
					description: t.description,
					status: t.status,
					createdAt: t.createdAt,
					priority: t.priority,
					parentTaskId: t.parentTaskId,
					depth: t.depth,
					isRoot: t.isRoot,
					isSystemTask: t.isSystemTask,
					category: t.category
				}));
				setTasks(normalized);
				setSummary(prog);
			} catch (e: any) {
				if (isAbortError(e)) {
					return; // ignore aborts from StrictMode dev double-invoke
				}
				setError(e?.message || 'Failed to load tasks');
			} finally {
				setLoading(false);
			}
		})();
		return () => { alive = false; ac.abort(); };
	}, []);

	// Live SSE to lightly flash updated tasks
	React.useEffect(() => {
		return connectServerEvents((msg) => {
			if (!msg || msg.type !== 'task') return;
			const id = String(msg.TaskId ?? msg.taskId ?? '').toLowerCase();
			if (!id) return;
			setTasks(prev => prev.map(t => t.id.toLowerCase() === id ? { ...t, status: msg.Status ?? t.status, flashUntil: Date.now() + 2000 } : t));
		});
	}, []);

	// Listen for SSE progress snapshots
	React.useEffect(() => {
		return connectServerEvents((msg) => {
			if (!msg || msg.type !== 'progress') return;
			const s = msg.summary || {};
			setSummary({
				total: s.total ?? 0,
				pending: s.pending ?? 0,
				analyzing: s.analyzing ?? 0,
				executing: s.executing ?? 0,
				aggregating: s.aggregating ?? 0,
				completed: s.completed ?? 0,
				failed: s.failed ?? 0,
			});
		});
	}, []);

	return (
		<div>
			<div className="mb-3 flex items-center justify-between">
				<h1 className="text-lg font-semibold">Tasks</h1>
				{summary && (
					<div className="text-xs text-slate-600 flex items-center gap-3">
						<span>total {summary.total ?? 0}</span>
						<span>running {(summary.analyzing ?? 0) + (summary.executing ?? 0) + (summary.aggregating ?? 0)}</span>
						<span>completed {summary.completed ?? 0}</span>
						<span>failed {summary.failed ?? 0}</span>
					</div>
				)}
			</div>
			{loading ? (
				<div className="p-3 border rounded bg-white">Loading…</div>
			) : error ? (
				<div className="p-3 border rounded bg-red-50 text-red-700 text-sm">{error}</div>
			) : (
				<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
					{tasks.map(t => (
						<TaskCard key={t.id} task={t} onSelect={(id) => setSelectedId(id)} />
					))}
				</div>
			)}
			<TaskDetailsModal open={!!selectedId} taskId={selectedId} onClose={() => setSelectedId(null)} onSelect={(id) => setSelectedId(id)} />
		</div>
	);
};

