import React from 'react';
import { useParams } from 'react-router-dom';
import { marked } from 'marked';

async function fetchReport(id: string, signal?: AbortSignal) {
	const res = await fetch(`/api/reports/${id}`, { signal });
	if (!res.ok) throw new Error(await res.text());
	return res.json() as Promise<{ markdown: string }>;
}

export const ReportPage: React.FC = () => {
	const { id = '' } = useParams();
	const [loading, setLoading] = React.useState(true);
	const [error, setError] = React.useState('');
	const [markdown, setMarkdown] = React.useState('');
	const [html, setHtml] = React.useState('');

	React.useEffect(() => {
		let alive = true;
		const ac = new AbortController();
		(async () => {
			try {
				setLoading(true);
				setError('');
				const data = await fetchReport(id, ac.signal);
				if (!alive) return;
				setMarkdown(String(data.markdown ?? ''));
			} catch (e: any) {
				if (e?.name === 'AbortError' || /aborted/i.test(e?.message || '')) {
					return; // ignore dev aborts
				}
				setError(e?.message || 'Failed to load report');
			} finally {
				setLoading(false);
			}
		})();
		return () => { alive = false; ac.abort(); };
	}, [id]);

	React.useEffect(() => {
		let cancelled = false;
		(async () => {
			try {
				const out = marked.parse(markdown);
				const htmlStr = typeof out === 'string' ? out : await out; // marked v9+ can return Promise
				if (!cancelled) setHtml(htmlStr);
			} catch {
				if (!cancelled) setHtml(markdown);
			}
		})();
		return () => { cancelled = true; };
	}, [markdown]);

	const download = React.useCallback(() => {
		window.location.href = `/api/reports/${id}/download?format=md`;
	}, [id]);

	return (
		<main className="p-2">
			<h1 className="text-lg font-semibold">Task Report</h1>
			<p className="text-sm text-slate-600">Task {id}</p>
			{loading ? (
				<div className="mt-4 p-4 border rounded bg-white animate-pulse">Loading report...</div>
			) : error ? (
				<div className="mt-4 p-3 border rounded bg-red-50 text-red-700 text-sm">{error}</div>
			) : (
				<>
					<div className="mt-4 flex items-center gap-2">
						<button className="px-3 py-1.5 text-xs rounded bg-slate-800 text-white hover:bg-slate-900" onClick={download}>Download Markdown</button>
					</div>
					<article className="mt-4 prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: html }} />
				</>
			)}
		</main>
	);
};

