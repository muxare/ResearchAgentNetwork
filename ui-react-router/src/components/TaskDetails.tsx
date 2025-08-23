import React from 'react';

type TaskDetailsData = {
	id: string;
	description?: string;
	status?: string;
	priority?: number;
	parentTaskId?: string;
};

type TimelineItem = { timestampUtc: string; eventType: string; status: string; message?: string };

async function fetchJson<T>(url: string, signal?: AbortSignal): Promise<T> {
	const res = await fetch(url, { signal });
	if (!res.ok) throw new Error(await res.text());
	return res.json();
}

export const TaskDetails: React.FC<{ taskId: string | null; onSelect?: (id: string) => void }> = ({ taskId, onSelect }) => {
	const [loading, setLoading] = React.useState(false);
	const [raw, setRaw] = React.useState('');
	const [meta, setMeta] = React.useState<TaskDetailsData | null>(null);
	const [err, setErr] = React.useState('');
	const [tab, setTab] = React.useState<'overview' | 'report' | 'raw' | 'events' | 'timeline'>('overview');
	const [timeline, setTimeline] = React.useState<TimelineItem[]>([]);
	const [timelineErr, setTimelineErr] = React.useState('');
	const [includeChildren, setIncludeChildren] = React.useState(false);
	const [children, setChildren] = React.useState<Array<{ id: string; description: string; status: string }>>([]);
	const [html, setHtml] = React.useState('');

	const scheduleParse = React.useCallback(() => {
		let cancelled = false;
		(async () => {
			try {
				const mod: any = await import('marked');
				const m = mod?.marked ?? mod?.default ?? mod;
				const out = m?.parse ? m.parse(raw) : String(raw ?? '');
				const htmlStr = typeof out === 'string' ? out : await out;
				if (!cancelled) setHtml(htmlStr);
			} catch {
				if (!cancelled) setHtml(String(raw ?? ''));
			}
		})();
		return () => { cancelled = true; };
	}, [raw]);

	React.useEffect(() => {
		if (tab === 'report') {
			return scheduleParse();
		}
	}, [tab, scheduleParse]);

	React.useEffect(() => {
		if (!taskId) return;
		let alive = true;
		const ac = new AbortController();
		(async () => {
			try {
				setLoading(true); setErr(''); setHtml('');
				const [taskRes, repRes, childrenRes, eventsRes] = await Promise.all([
					fetch(`/api/tasks/${taskId}`, { signal: ac.signal }),
					fetch(`/api/tasks/${taskId}/report`, { signal: ac.signal }),
					fetch(`/api/tasks/${taskId}/children`, { signal: ac.signal }),
					fetch(`/api/tasks/${taskId}/events?includeChildren=${includeChildren ? 'true' : 'false'}`, { signal: ac.signal })
				]);
				if (!alive) return;
				if (taskRes.ok) setMeta(await taskRes.json());
				setRaw(repRes.ok ? await repRes.text() : 'No report available');
				setChildren(childrenRes.ok ? await childrenRes.json() : []);
				if (eventsRes.ok) {
					const evs = await eventsRes.json();
					setTimeline(Array.isArray(evs) ? evs.map((e: any) => ({ timestampUtc: e.timestampUtc, eventType: e.eventType, status: e.status, message: e.detailsJson ? `json:${e.detailsJson}` : e.message })) : []);
					setTimelineErr('');
				} else {
					setTimeline([]); setTimelineErr('Failed to load timeline');
				}
			} catch (e: any) {
				setErr(e?.message || 'Failed to load details');
			} finally {
				setLoading(false);
			}
		})();
		return () => { alive = false; ac.abort(); };
	}, [taskId, includeChildren]);

	const action = React.useCallback(async (kind: 'retry' | 'cancel' | 'force') => {
		if (!taskId) return;
		try {
			await fetch(`/api/tasks/${taskId}`, { method: 'PATCH', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ action: kind }) });
		} catch {}
	}, [taskId]);

	function parseMaybeJson(x?: string): any | null {
		if (!x) return null;
		const s = String(x);
		if (s.startsWith('json:')) {
			try { return JSON.parse(s.substring(5)); } catch { return null; }
		}
		return null;
	}

	if (!taskId) return <div className="text-sm text-gray-500">Select a task…</div>;
	if (loading) return <div className="text-sm text-gray-500">Loading…</div>;

	return (
		<div>
			{err && <div className="text-sm text-red-600">{err}</div>}
			{meta && (
				<>
					<div className="mb-2">
						<div className="font-semibold">{meta.description}</div>
						<div className="text-xs text-gray-500">{meta.status} • prio {meta.priority}</div>
						<div className="text-xs text-gray-400">{meta.id}</div>
					</div>
					<div className="mb-3 border-b">
						<nav className="flex gap-2 text-xs">
							<button type="button" className={`px-2 py-1 ${tab==='overview' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onClick={() => setTab('overview')}>Overview</button>
							<button type="button" className={`px-2 py-1 ${tab==='report' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onClick={() => setTab('report')}>Report</button>
							<button type="button" className={`px-2 py-1 ${tab==='raw' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onClick={() => setTab('raw')}>Raw</button>
							<button type="button" className={`px-2 py-1 ${tab==='events' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onClick={() => setTab('events')}>Events</button>
							<button type="button" className={`px-2 py-1 ${tab==='timeline' ? 'border-b-2 border-blue-600 text-blue-700' : 'text-gray-600'}`} onClick={() => setTab('timeline')}>Timeline</button>
						</nav>
					</div>
					{tab === 'overview' && (
						<div>
							<div className="mb-4 text-xs text-gray-600 flex flex-wrap gap-2">
								{meta.parentTaskId && (
									<button type="button" className="px-2 py-0.5 rounded border bg-gray-50 hover:bg-gray-100" onClick={() => onSelect?.(meta.parentTaskId!)}>↑ Parent</button>
								)}
								{children.length > 0 ? (
									<>
										<span className="text-gray-500">Children:</span>
										{children.map(c => (
											<button key={c.id} type="button" className="px-2 py-0.5 rounded border bg-gray-50 hover:bg-gray-100" onClick={() => onSelect?.(c.id)}>{c.status}: {c.description}</button>
										))}
									</>
								) : (
									<span className="text-gray-400">No children</span>
								)}
							</div>
							<div className="flex gap-2 mb-4">
								<button type="button" className="px-2 py-1 rounded border text-xs bg-blue-50 hover:bg-blue-100" onClick={() => action('retry')}>Retry</button>
								<button type="button" className="px-2 py-1 rounded border text-xs bg-orange-50 hover:bg-orange-100" onClick={() => action('cancel')}>Cancel</button>
								<button type="button" className="px-2 py-1 rounded border text-xs bg-purple-50 hover:bg-purple-100" onClick={() => action('force')}>Force Execute</button>
							</div>
						</div>
					)}
					{tab === 'report' && (
						<div>
							<h3 className="text-sm font-semibold">Report (rendered)</h3>
							<div className="flex items-center gap-2 mt-2">
								<a className="text-xs px-2 py-1 rounded bg-slate-100 hover:bg-slate-200" href={`/api/reports/${meta.id}/download?format=md`} target="_blank" rel="noreferrer">Download MD</a>
								<a className="text-xs px-2 py-1 rounded bg-blue-600 text-white hover:bg-blue-700" href={`/tasks/${meta.id}/report`}>Open In-App Viewer</a>
							</div>
							<div className="prose max-w-none mt-2" dangerouslySetInnerHTML={{ __html: html }} />
						</div>
					)}
					{tab === 'raw' && (
						<div>
							<h3 className="text-sm font-semibold">Report (raw)</h3>
							<pre className="text-xs text-gray-600 mt-2 whitespace-pre-wrap">{raw}</pre>
						</div>
					)}
					{tab === 'events' && (
						<div>
							<h3 className="text-sm font-semibold">Events</h3>
							<div className="text-xs text-gray-500 mt-2">Use the timeline tab for persisted events.</div>
						</div>
					)}
					{tab === 'timeline' && (
						<div>
							<div className="flex items-center justify-between">
								<h3 className="text-sm font-semibold">Timeline</h3>
								<div className="flex items-center gap-3">
									<a className="text-xs text-blue-700 hover:underline" target="_blank" href={`/api/tasks/${taskId}/report`}>open report</a>
									<label className="text-xs text-gray-600 flex items-center gap-1">
										<input type="checkbox" checked={includeChildren} onChange={e => setIncludeChildren((e.target as HTMLInputElement).checked)} /> include children
									</label>
								</div>
							</div>
							{timelineErr ? (
								<div className="text-xs text-red-600 mt-2">{timelineErr}</div>
							) : timeline.length === 0 ? (
								<div className="text-xs text-gray-500 mt-2">No events.</div>
							) : (
								<ul className="mt-2 space-y-1 max-h-64 overflow-auto text-xs text-gray-700">
									{timeline.map((it, idx) => (
										<li key={idx} className="flex items-start gap-2">
											<span className="text-gray-500 shrink-0 w-40">{new Date(it.timestampUtc).toLocaleString()}</span>
											<span className="shrink-0 w-28">{it.eventType}</span>
											<span className="shrink-0 w-24">{it.status}</span>
											{parseMaybeJson(it.message) ? (
												<pre className="truncate max-w-[28rem] text-[10px] text-gray-600">{JSON.stringify(parseMaybeJson(it.message), null, 2)}</pre>
											) : (
												<span className="truncate">{it.message}</span>
											)}
										</li>
									))}
								</ul>
							)}
						</div>
					)}
				</>
			)}
		</div>
	);
};