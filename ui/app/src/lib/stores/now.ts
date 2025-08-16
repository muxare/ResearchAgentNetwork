import { readable } from 'svelte/store';

// Shared timer to avoid creating one interval per TaskCard
export const now = readable<number>(Date.now(), (set) => {
	const id = setInterval(() => set(Date.now()), 250);
	return () => clearInterval(id);
});

