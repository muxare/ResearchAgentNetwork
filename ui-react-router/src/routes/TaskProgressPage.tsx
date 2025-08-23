import React from 'react';
import { useParams } from 'react-router-dom';
import { KanbanBoard } from '../kanban/KanbanBoard';
import { TaskItem } from '../components/TaskCard';

async function fetchJson<T>(url: string, signal?: AbortSignal): Promise<T> {
	const res = await fetch(url, { signal });
	if (!res.ok) throw new Error(await res.text());
	return res.json();
}

export const TaskProgressPage: React.FC = () => {
	const { id = '' } = useParams();
	const [rootMeta, setRootMeta] = React.useState<any | null>(null);
	const [tasks, setTasks] = React.useState<TaskItem[]>([]);
	const [loading, setLoading] = React.useState<boolean>(true);
	const [error, setError] = React.useState<string>('');

	React.useEffect(() => {
		let alive = true;
		const ac = new AbortController();
		(async () => {
			try {
				setLoading(true); setError('');
				const [meta, all] = await Promise.all([
					fetchJson<any>(`/api/tasks/${id}`, ac.signal),
					fetchJson<any[]>(`/api/tasks`, ac.signal)
				]);
				if (!alive) return;
				setRootMeta(meta);
				// Show all tasks for now; in future filter by hierarchy if needed
				const normalized: TaskItem[] = (all ?? []).map((t: any) => ({
					id: t.id, description: t.description, status: t.status, createdAt: t.createdAt, priority: t.priority, parentTaskId: t.parentTaskId, depth: t.depth, isRoot: t.isRoot, isSystemTask: t.isSystemTask, category: t.category
				}));
				setTasks(normalized);
			} catch (e: any) {
				setError(e?.message || 'Failed to load task progress');
			} finally {
				setLoading(false);
			}
		})();
		return () => { alive = false; ac.abort(); };
	}, [id]);

	return (
		<div className="space-y-4">
			{rootMeta && (
				<div className="bg-white rounded border p-3">
					<div className="text-sm text-gray-500">Root Task</div>
					<div className="font-semibold">{rootMeta.description}</div>
					<div className="text-xs text-gray-500">{rootMeta.id} • prio {rootMeta.priority}</div>
				</div>
			)}
			{error && <div className="p-3 border rounded bg-red-50 text-red-700 text-sm">{error}</div>}
			<KanbanBoard tasks={tasks} loading={loading} onSelect={() => {}} />
		</div>
	);
};

