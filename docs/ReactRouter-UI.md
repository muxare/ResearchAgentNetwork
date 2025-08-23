## React Router UI (Tailwind v4)

This document describes the new React Router frontend located at `ui-react-router`.

### Architecture
- **Build**: Vite + TypeScript + `@vitejs/plugin-react`
- **Styling**: Tailwind CSS v4 via `@tailwindcss/vite` plugin. Only `@import "tailwindcss";` is needed in `src/index.css`.
- **Routing**: `react-router-dom` with a root layout and child routes:
  - `/` → tasks list
  - `/tasks/:id/report` → report viewer
- **Components**:
  - `TaskCard` — compact task tile
  - `TaskDetails` — details tabs (overview/report/raw/timeline)
  - `TaskDetailsModal` — modal wrapper used by the tasks list
  - `src/lib/events.ts` — SSE helper for `/api/events`

### Data Flow
- On Tasks page mount, the app fetches:
  - `GET /api/tasks` → tasks list
  - `GET /api/progress` → summary counters
- Selecting a task opens `TaskDetailsModal` which fetches, in parallel:
  - `GET /api/tasks/{id}` → task meta
  - `GET /api/tasks/{id}/children` → child tasks
  - `GET /api/tasks/{id}/report` → raw markdown
  - `GET /api/tasks/{id}/events?includeChildren={bool}` → timeline
- Actions:
  - `PATCH /api/tasks/{id}` with `{ action: 'retry' | 'cancel' | 'force' }`
- Reports (viewer route):
  - `GET /api/reports/{id}` returns `{ markdown }`
  - `GET /api/reports/{id}/download?format=md` for download
- Live updates:
  - `GET /api/events` SSE → soft updates for task rows (status + flash)

### Execution Flow
1. User loads `/`.
2. App renders `TasksPage`, fetches tasks and summary.
3. User clicks a task → opens `TaskDetailsModal` and loads details.
4. Tabs allow switching between overview/report/raw/timeline.
5. Report viewer is also accessible via route `/tasks/{id}/report`.

### Setup & Run
1. `cd ui-react-router`
2. `npm install`
3. `npm run dev` — opens Vite dev server

### Testing
- Manual checks:
  - Tasks list renders and shows counts
  - Selecting task opens modal and loads details
  - Actions (retry/cancel/force) respond without errors
  - Report viewer renders markdown
  - Live status flashes when SSE events arrive

### Notes
- This UI assumes APIs served from the same origin as the backend.
- Tailwind v4 is already wired via Vite plugin; no `tailwind.config.js` is needed.

