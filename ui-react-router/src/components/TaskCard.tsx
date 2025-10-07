import React from 'react';

export type TaskItem = {
	id: string;
	description: string;
	status: string;
	createdAt?: string;
	priority?: number;
	parentTaskId?: string;
	depth?: number;
	isRoot?: boolean;
	isSystemTask?: boolean;
	category?: string;
	_stackCount?: number;
	flashUntil?: number;
};

function formatDate(iso?: string): string {
	if (!iso) return '';
	try { return new Date(iso).toLocaleString(); } catch { return iso; }
}

const statusToColor: Record<string, string> = {
	Pending: 'badge badge-muted',
	Analyzing: 'badge badge-info',
	Executing: 'badge bg-purple-100 text-purple-700',
	Aggregating: 'badge badge-warn',
	Completed: 'badge badge-success',
	Failed: 'badge badge-danger'
};

export const TaskCard: React.FC<{ task: TaskItem; onSelect?: (id: string) => void }> = ({ task, onSelect }) => {
	return (
		<button type="button"
			className={`relative w-full text-left p-2 card group text-xs ${task.flashUntil && task.flashUntil > Date.now() ? 'ring-2 ring-offset-1 ring-yellow-300' : ''}`}
			onClick={() => onSelect?.(task.id)}>
			{(task._stackCount ?? 0) > 0 && (
				<span className="pointer-events-none absolute inset-0 -z-10">
					<span className="absolute inset-0 translate-x-1 translate-y-1 rounded-lg border bg-white/90 shadow-sm"></span>
					<span className="absolute inset-0 translate-x-2 translate-y-2 rounded-lg border bg-white/80 shadow-sm"></span>
				</span>
			)}
			<div className="flex items-center justify-between">
				<span className="text-[10px] text-gray-500 font-mono truncate max-w-[96px]">{task.id}</span>
				<span className={`${statusToColor[task.status] ?? 'badge badge-muted'}`}>
					{task.status}
					{(task._stackCount ?? 0) > 0 && (
						<span className="ml-1 text-[9px] text-gray-700 align-middle">(+{task._stackCount})</span>
					)}
				</span>
			</div>
			<div className="mt-1 text-xs font-medium leading-tight text-slate-800 line-clamp-2 group-hover:line-clamp-4">
				{task.isRoot ? (
					<span className="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-emerald-50 text-emerald-700 border border-emerald-200">root</span>
				) : (typeof task.depth === 'number' && task.depth > 0) ? (
					<span className="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-slate-50 text-slate-700 border border-slate-200">lvl {task.depth}</span>
				) : null}
				{task.isSystemTask && (
					<span className="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-indigo-50 text-indigo-700 border border-indigo-200">system</span>
				)}
				{(task.category ?? '').toLowerCase() === 'finalization' && (
					<span className="inline-flex items-center gap-1 mr-1 text-[10px] px-1 py-0.5 rounded bg-cyan-50 text-cyan-700 border border-cyan-200">finalization</span>
				)}
				{task.description}
			</div>
			<div className="mt-1 flex items-center gap-2 text-[10px] text-gray-500">
				{typeof task.priority === 'number' && (
					<span className="px-1.5 py-0.5 rounded bg-slate-100">prio {task.priority}</span>
				)}
				{task.createdAt && <span>{formatDate(task.createdAt)}</span>}
			</div>
			{(task.status === 'Analyzing' || task.status === 'Executing' || task.status === 'Aggregating') && (
				<div className="mt-2 flex items-center gap-2 text-[10px]">
					<span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-yellow-50 text-yellow-700 border border-yellow-200">
						<span className="relative flex h-2 w-2">
							<span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-yellow-400 opacity-75"></span>
							<span className="relative inline-flex rounded-full h-2 w-2 bg-yellow-500"></span>
						</span>
						LLM active
					</span>
				</div>
			)}
		</button>
	);
};

