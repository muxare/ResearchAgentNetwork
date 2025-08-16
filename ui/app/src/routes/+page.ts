import type { PageLoad } from './$types';

export const ssr = false;
export const prerender = false;

export const load: PageLoad = async ({ fetch }) => {
	try {
		const res = await fetch('/api/tasks');
		if (!res.ok) throw new Error(await res.text());
		const tasks = await res.json();
		return { tasks };
	} catch {
		return { tasks: [] };
	}
};

