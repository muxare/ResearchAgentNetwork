import type { PageLoad } from './$types';

export const load: PageLoad = async ({ params, fetch }) => {
  const id = params.id;
  try {
    const res = await fetch(`/api/reports/${id}`);
    if (!res.ok) return { report: null } as any;
    const data = await res.json();
    return { report: data } as any;
  } catch {
    return { report: null } as any;
  }
};

