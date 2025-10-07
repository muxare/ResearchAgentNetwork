import React from 'react';
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';

export const AppLayout: React.FC = () => {
	const navigate = useNavigate();
	return (
		<div className="h-screen flex">
			{/* Sidebar */}
			<aside className="w-64 flex-shrink-0 bg-white/90 dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700 flex flex-col backdrop-blur">
				<div className="h-16 flex items-center justify-center border-b border-gray-200 dark:border-gray-700">
					<Link to="/" className="text-xl font-bold text-indigo-600 dark:text-indigo-400 tracking-tight">ResearchAI</Link>
				</div>
				<nav className="flex-1 px-3 py-4 space-y-2">
					<h2 className="px-2 text-xs font-semibold text-gray-500 uppercase tracking-wider">Research</h2>
					<NavLink to="/chat" className={({ isActive }) => `block px-3 py-2 text-sm rounded-lg ${isActive ? 'bg-gray-200 dark:bg-gray-700 text-slate-900 dark:text-white font-medium' : 'text-slate-700 dark:text-slate-200 hover:bg-gray-100 dark:hover:bg-gray-700'}`}>Chat</NavLink>
					<NavLink to="/research" className={({ isActive }) => `block px-3 py-2 text-sm rounded-lg ${isActive ? 'bg-gray-200 dark:bg-gray-700 text-slate-900 dark:text-white font-medium' : 'text-slate-700 dark:text-slate-200 hover:bg-gray-100 dark:hover:bg-gray-700'}`}>All Queries</NavLink>
					<h2 className="px-2 pt-4 text-xs font-semibold text-gray-500 uppercase tracking-wider">System</h2>
					<NavLink to="/monitoring" className={({ isActive }) => `block px-3 py-2 text-sm rounded-lg ${isActive ? 'bg-gray-200 dark:bg-gray-700 text-slate-900 dark:text-white font-medium' : 'text-slate-700 dark:text-slate-200 hover:bg-gray-100 dark:hover:bg-gray-700'}`}>Monitoring</NavLink>
					<NavLink to="/admin" className={({ isActive }) => `block px-3 py-2 text-sm rounded-lg ${isActive ? 'bg-gray-200 dark:bg-gray-700 text-slate-900 dark:text-white font-medium' : 'text-slate-700 dark:text-slate-200 hover:bg-gray-100 dark:hover:bg-gray-700'}`}>User Admin</NavLink>
					<div className="pt-4">
						<NavLink to="/" end className={({ isActive }) => `block px-3 py-2 text-sm rounded-lg ${isActive ? 'bg-gray-200 dark:bg-gray-700 text-slate-900 dark:text-white font-medium' : 'text-slate-700 dark:text-slate-200 hover:bg-gray-100 dark:hover:bg-gray-700'}`}>Tasks</NavLink>
						<RootTasksMenu onNavigate={(id) => navigate(`/tasks/${id}/progress`)} />
					</div>
				</nav>
			</aside>

			{/* Main */}
			<div className="flex-1 flex flex-col overflow-hidden">
				<header className="h-16 bg-white/90 dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700 flex items-center px-6 justify-between backdrop-blur">
					<h2 className="text-lg font-semibold tracking-tight">Research Agent Network</h2>
					<div />
				</header>
				<main className="flex-1 overflow-y-auto p-6">
					<Outlet />
				</main>
			</div>
		</div>
	);
};

// RootTasksMenu lists root tasks and allows navigating to a root's progress page
const RootTasksMenu: React.FC<{ onNavigate?: (id: string) => void }> = ({ onNavigate }) => {
	const [roots, setRoots] = React.useState<Array<{ id: string; description: string }>>([]);
	const [error, setError] = React.useState('');
	const [loading, setLoading] = React.useState(false);
	const [skip, setSkip] = React.useState(0);
	const [hasMore, setHasMore] = React.useState(true);
	const listRef = React.useRef<HTMLDivElement | null>(null);
	const PAGE = 50;

	const loadMore = React.useCallback(async () => {
		if (loading || !hasMore) return;
		setLoading(true);
		try {
			const res = await fetch(`/api/tasks/root?top=${PAGE}&skip=${skip}`);
			if (!res.ok) throw new Error(await res.text());
			const list = await res.json();
			const batch: Array<{ id: string; description: string; parentTaskId?: string | null }> = Array.isArray(list) ? list : [];
			const mapped = batch
				.filter(t => !t.parentTaskId)
				.map(t => ({ id: String((t as any).id), description: String((t as any).description ?? '') }));
			setRoots(prev => {
				const existing = new Set(prev.map(x => x.id.toLowerCase()));
				const merged = [...prev];
				for (const m of mapped) if (!existing.has(m.id.toLowerCase())) merged.push(m);
				return merged;
			});
			setSkip(prev => prev + mapped.length);
			setHasMore(mapped.length === PAGE);
			setError('');
		} catch (e: any) {
			setError(e?.message || 'Failed to load root tasks');
			setHasMore(false);
		} finally {
			setLoading(false);
		}
	}, [loading, hasMore, skip]);

	React.useEffect(() => { void loadMore(); }, []);

	React.useEffect(() => {
		const el = listRef.current;
		if (!el) return;
		const onScroll = () => {
			if (el.scrollTop + el.clientHeight >= el.scrollHeight - 4) {
				void loadMore();
			}
		};
		el.addEventListener('scroll', onScroll);
		return () => el.removeEventListener('scroll', onScroll);
	}, [loadMore]);

	return (
		<div className="mt-2">
			{error && <div className="text-[10px] text-red-600 px-2 mb-1">{error}</div>}
			<div ref={listRef} className="space-y-1 max-h-72 overflow-auto pr-1">
				{roots.map(r => (
					<button key={r.id} type="button" title={r.description} className="w-full text-left px-3 py-1 text-xs rounded truncate text-slate-800 dark:text-slate-100 hover:bg-gray-100 dark:hover:bg-gray-700" onClick={() => onNavigate?.(r.id)}>
						{r.description}
					</button>
				))}
				{loading && <div className="text-[10px] text-slate-500 px-3 py-1">Loading…</div>}
				{!loading && hasMore && (
					<button type="button" className="w-full text-left px-3 py-1 text-[10px] text-indigo-600 hover:underline" onClick={() => loadMore()}>Load more…</button>
				)}
			</div>
		</div>
	);
};

