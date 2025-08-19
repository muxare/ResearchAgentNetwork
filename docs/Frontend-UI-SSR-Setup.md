# Frontend UI (SSR) Setup

This document describes the new server-rendered SvelteKit UI under `ui-2` and how it integrates with the backend.

## Architecture

- App lives in `ui-2` and is a standard SvelteKit SSR app (no `ssr=false`).
- Uses Tailwind v4 via Vite plugin, same as `ui/app`.
- Vite dev server proxies `/api` and `/admin` to the ASP.NET backend at `http://localhost:5000`.
- Data is fetched on the server in `+page.server.ts` for initial render.
- Live updates use SSE via `/api/events` with a client-only guard to avoid running on the server.

## Data Flow

- Initial tasks are loaded in `src/routes/+page.server.ts` from `/api/tasks` and passed to the page.
- The page initializes the client store with initial tasks and starts the SSE subscription.
- The SSE feed emits task updates which the store merges into the list.
- Creating a task is done via a SvelteKit action that POSTs to `/api/tasks` and then refreshes tasks.

## Execution Flow

1. Server receives request to `/` → `+page.server.ts` fetches tasks.
2. SSR renders HTML with initial task list.
3. On client hydration, the SSE subscription starts and streams updates.
4. Form submit posts back to `?/create` action, which calls backend and returns status.

## How to Run

1. Start the backend: `dotnet run --project ResearchAgentNetwork.Web` (serves at `http://localhost:5000`).
2. Run the SSR UI:
   - `cd ui-2`
   - `npm install` (or `pnpm i`)
   - `npm run dev`
3. Open the UI dev server URL (typically `http://localhost:5173`).

The dev server proxies API calls and SSE to the backend.

## Testing

- Create a task using the form and verify it appears and updates.
- Open two browser windows and confirm SSE updates reflect in real time.
- Verify server-side render by viewing page source (should contain initial HTML with tasks).

## Notes

- This SSR app intentionally keeps UI minimal to match functionality from `ui/app` while avoiding SPA mode.
- We can incrementally migrate shared components from `ui/app/src/lib/components` if needed.

