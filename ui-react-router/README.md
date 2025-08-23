# React Router UI (Tailwind v4)

This is a React + Vite + React Router frontend that mirrors the SvelteKit UI. It uses Tailwind CSS v4 via the `@tailwindcss/vite` plugin.

## Scripts

- `npm run dev` — start dev server
- `npm run build` — production build
- `npm run preview` — preview build

## Environment

The app expects the backend to be running at the same origin, exposing endpoints such as:
- `GET /api/tasks` — list tasks
- `GET /api/tasks/:id` — task details
- `GET /api/tasks/:id/children` — child tasks
- `GET /api/tasks/:id/report` — raw markdown report (text/plain)
- `GET /api/reports/:id` — persisted report JSON { markdown }
- `PATCH /api/tasks/:id` — actions: `{ action: 'retry' | 'cancel' | 'force' }`
- `GET /api/progress` — progress summary
- `GET /api/events` — server-sent events stream

## Notes

- Tailwind v4 requires only `@import "tailwindcss";` in `src/index.css`.
- Vite plugin `@tailwindcss/vite` auto-injects Tailwind.

