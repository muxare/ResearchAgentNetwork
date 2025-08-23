import React from 'react';
import { TaskItem } from '../components/TaskCard';
import { VALID_STATUSES, normalizeStatus, loadOrder, saveOrder, reconcileOrder, orderTasksForStatus } from './kanbanOrder';

type Props = {
	tasks: TaskItem[];
	loading?: boolean;
	onSelect?: (id: string) => void;
	laneHeight?: string;
};

export const KanbanBoard: React.FC<Props> = ({ tasks, loading, onSelect, laneHeight }) => {
	const statuses = VALID_STATUSES as string[];
	const [order, setOrder] = React.useState<Record<string, string[]>>(() => loadOrder());
	const [filterStatus, setFilterStatus] = React.useState<string>('');
	const [filterText, setFilterText] = React.useState<string>('');
	const [laneHeightLocal, setLaneHeightLocal] = React.useState<string>('62vh');

	React.useEffect(() => {
		try {
			const a = localStorage.getItem('kanbanLaneHeight');
			if (a) setLaneHeightLocal(a);
			const b = localStorage.getItem('kanbanFilterStatus');
			if (b) setFilterStatus(b);
			const c = localStorage.getItem('kanbanFilterText');
			if (c) setFilterText(c);
		} catch {}
	}, []);

	function saveLaneHeight(v: string) { setLaneHeightLocal(v); try { localStorage.setItem('kanbanLaneHeight', v); } catch {} }
	function saveFilterStatus(v: string) { setFilterStatus(v); try { localStorage.setItem('kanbanFilterStatus', v); } catch {} }
	function saveFilterText(v: string) { setFilterText(v); try { localStorage.setItem('kanbanFilterText', v); } catch {} }

	function byStatus(status: string): TaskItem[] {
		const key = normalizeStatus(status);
		const q = filterText.trim().toLowerCase();
		let source = tasks;
		if (filterStatus) source = source.filter(t => normalizeStatus((t as any).status) === filterStatus);
		if (q) source = source.filter(t => (t.description?.toLowerCase().includes(q)) || ((t.id + '').toLowerCase().includes(q)));
		const list = source.map((t: any) => ({
			...t,
			status: normalizeStatus(t.status),
			isSystemTask: t.isSystemTask ?? false,
			category: t.category ?? ((t as any).metadata?.Category ?? (t as any).metadata?.category ?? '')
		})).filter(t => t.status === key);
		return orderTasksForStatus(key as any, list, order as any);
	}

	// reconcile order when tasks change
	React.useEffect(() => {
		const next = reconcileOrder(order as any, tasks as any);
		const equal = JSON.stringify(next) === JSON.stringify(order);
		if (!equal) {
			setOrder(next);
			saveOrder(next as any);
		}
	}, [tasks]);

	function handleReorder(status: string, ids: string[]) {
		const next = { ...(order as any), [status]: ids } as any;
		setOrder(next);
		saveOrder(next);
	}

	return (
		<div className="overflow-x-hidden">
			<div className="p-2 border-b bg-white/70 backdrop-blur sticky top-0 z-10">
				<div className="flex flex-wrap items-end gap-3 text-xs">
					<label className="text-sm">
						<div className="text-[11px] text-gray-600">Filter</div>
						<input className="border rounded-md px-2 py-1 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" value={filterText} onChange={e => saveFilterText((e.target as HTMLInputElement).value)} placeholder="search..." />
					</label>
					<label className="text-sm">
						<div className="text-[11px] text-gray-600">Status</div>
						<select className="border rounded-md px-2 py-1 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" value={filterStatus} onChange={e => saveFilterStatus((e.target as HTMLSelectElement).value)}>
							<option value="">All</option>
							{statuses.map(s => (<option key={s} value={s}>{s}</option>))}
						</select>
					</label>
					<label className="text-sm">
						<div className="text-[11px] text-gray-600">Lane Height</div>
						<input className="border rounded-md px-2 py-1 w-28 focus:ring-2 focus:ring-blue-500/30 focus:outline-none" value={laneHeightLocal} onChange={e => saveLaneHeight((e.target as HTMLInputElement).value)} placeholder="62vh or 480px" />
					</label>
				</div>
			</div>
			<div className="flex flex-wrap items-start gap-3 p-2 w-full">
				{statuses.map(s => (
					<KanbanColumn key={s} title={s} tasks={byStatus(s)} loading={loading} onSelect={onSelect} laneHeight={laneHeight ?? laneHeightLocal} onReorder={(ids) => handleReorder(s, ids)} />
				))}
			</div>
			{tasks.length === 0 && (
				<div className="text-sm text-gray-500 p-2">No tasks</div>
			)}
		</div>
	);
};

type ColumnProps = {
	title: string;
	tasks: TaskItem[];
	loading?: boolean;
	onSelect?: (id: string) => void;
	laneHeight?: string;
	onReorder?: (ids: string[]) => void;
};

// Simplified column without drag-drop for now; we can add dnd-kit later
const KanbanColumn: React.FC<ColumnProps> = ({ title, tasks, loading, onSelect, laneHeight }) => {
	return (
		<section className="flex flex-col gap-2 bg-white/80 rounded-xl p-3 border shadow-sm w-[48%] sm:w-[48%] md:w-[31%] lg:w-[23%] xl:w-[16%] min-w-[220px]">
			<header className="px-2 py-1 sticky top-0 z-10 bg-white/70 backdrop-blur border-b rounded-t-xl flex items-center gap-2">
				<span className={
					title === 'Pending' ? 'w-2 h-2 rounded-full bg-gray-500' :
					title === 'Analyzing' ? 'w-2 h-2 rounded-full bg-blue-600' :
					title === 'Executing' ? 'w-2 h-2 rounded-full bg-purple-600' :
					title === 'Aggregating' ? 'w-2 h-2 rounded-full bg-orange-500' :
					title === 'Completed' ? 'w-2 h-2 rounded-full bg-green-600' :
					'w-2 h-2 rounded-full bg-red-600'
				}></span>
				<h2 className="text-sm font-semibold">{title} <span className="text-xs text-gray-500">({tasks.length})</span></h2>
			</header>
			<div className="flex flex-col gap-2 overflow-auto" style={{ maxHeight: laneHeight ?? '60vh' }}>
				{loading ? (
					Array.from({ length: 3 }).map((_, i) => (
						<div key={i} className="p-3 rounded border bg-white animate-pulse">
							<div className="h-3 bg-gray-200 rounded w-1/2"></div>
							<div className="h-4 bg-gray-200 rounded w-5/6 mt-2"></div>
							<div className="h-3 bg-gray-200 rounded w-1/3 mt-2"></div>
						</div>
					))
				) : tasks.length === 0 ? (
					<div className="text-xs text-gray-500 p-2">No tasks in {title}</div>
				) : (
					tasks.map(t => (
						<div key={t.id} onClick={() => onSelect?.(t.id)}>
							{/* Reuse styles from TaskCard */}
							<div className="relative w-full text-left p-2 rounded-lg border bg-white hover:shadow-md transition group text-xs">
								<div className="flex items-center justify-between">
									<span className="text-[10px] text-gray-500 font-mono truncate max-w-[96px]">{t.id}</span>
									<span className="text-[10px] px-1 py-0.5 rounded-full shadow bg-gray-200">{t.status}</span>
								</div>
								<div className="mt-1 text-xs font-medium leading-tight text-slate-800 line-clamp-2 group-hover:line-clamp-4">{t.description}</div>
							</div>
						</div>
					))
				)}
			</div>
		</section>
	);
};

