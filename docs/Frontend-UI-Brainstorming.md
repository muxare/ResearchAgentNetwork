<!-- markdownlint-disable MD041 -->
## Frontend UI Brainstorming: Svelte + Kanban Board for Tasks

This document explores a Svelte-based frontend to visualize and manage research tasks as a Kanban board aligned to task states. It proposes architecture options, data/execution flows, component structure, and a phased plan that respects PR size and testability constraints.

### Goals

- Replace current static UI with a Svelte frontend
- Visualize tasks as a Kanban board across states: Pending, Analyzing, Executing, Aggregating, Completed, Failed
- Preserve and enhance existing capabilities: submit tasks, filter/search, view details, live events (SSE), view/download Markdown report, admin lists, runtime settings
- Keep implementation incremental and small per PR

### Current Backend UI/API Snapshot

- Status enum: `Pending`, `Analyzing`, `Executing`, `Aggregating`, `Completed`, `Failed`
- APIs:
  - POST `/api/tasks` (submit)
  - GET `/api/tasks` (list), GET `/api/tasks/{id}`, GET `/api/tasks/{id}/children`
  - PATCH `/api/tasks/{id}` with `{ action: 'retry'|'cancel'|'force' }`
  - GET `/api/progress`, GET/POST `/api/settings`
  - GET `/api/tasks/{id}/report`, GET `/api/tasks/{id}/report/persisted`
  - Admin: GET `/admin/tasks`, `/admin/events`, report download endpoints
- Live updates: SSE via GET `/api/events` (progress + task events)

### Kanban Board Concept

- Columns map 1:1 to `TaskStatus` values
- Cards show: description, status badge, created time, priority; quick actions (Retry/Cancel/Force) where applicable
- Selection opens a details panel (report preview, raw markdown, events, actions)
- Filters: text and status filters applied across all columns
- Optional swimlanes: by parent task or priority bands
- Drag and drop: visual reordering within a column; optional priority adjustment on drop (status is orchestrator-driven; disallow direct status changes to avoid invalid transitions)

### Architecture Options

Option A: SvelteKit app

- Pros: File-based routing, SSR/CSR flexibility, first-class endpoints (if needed), easy code-splitting, great DX
- Integration: Dev server proxies `/api/*` to ASP.NET backend; production uses `@sveltejs/adapter-static` initially (static export) and serves from `wwwroot` to avoid introducing a second server. Can switch to SSR adapter later if needed.

Option B: Svelte (Vite) SPA

- Pros: Simpler, minimal footprint; easy to build-to-static and drop in `wwwroot`
- Integration: Dev server proxies `/api/*`; production build copied to `ResearchAgentNetwork.Web/wwwroot`

Recommendation: Choose SvelteKit with `adapter-static` initially to keep a single-server deployment while gaining SvelteKit DX and routing. Revisit SSR later if requirements emerge.

### Data and Execution Flow (Frontend)

1) Initial load
   - Fetch settings `/api/settings`
   - Fetch tasks `/api/tasks`
   - Open SSE `/api/events` to receive progress and task updates
2) User actions
   - Submit task → POST `/api/tasks` → refresh tasks
   - Quick actions on a card → PATCH `/api/tasks/{id}` with action
   - View report → GET `/api/tasks/{id}/report` (render Markdown)
   - Load persisted report → GET `/api/tasks/{id}/report/persisted`
   - Settings update → POST `/api/settings`
3) Live updates
   - On SSE `task` event, merge patch into local store; update the relevant Kanban card/column

### Component Breakdown (SPA)

- `App.svelte`: shell, routes, global toasts/dialogs
- `KanbanBoard.svelte`: orchestrates columns, filtering, virtualization
- `KanbanColumn.svelte`: column header, droppable list, lazy windowing for many cards
- `TaskCard.svelte`: description, badges, created, priority, quick actions
- `TaskDetails.svelte`: side panel with details, events, actions
- `ReportViewer.svelte`: Markdown rendering, download/copy actions
- `SettingsPanel.svelte`: runtime settings form
- `AdminPanel.svelte`: optional admin lists in a separate view/tab
- `stores/tasks.ts`: writable store managing normalized tasks map and derived lists by status
- `lib/api.ts`: API client wrappers; SSE subscription helper

Libraries:

- DnD: `svelte-dnd-action` for reordering and potential priority adjustments
- Markdown: `marked` or `micromark`; keep consistent with current behavior
- Styling: Tailwind CSS (selected). Use a small palette for status colors and utilities for layout.

### UX Details and Refinements

- Column order: Pending → Analyzing → Executing → Aggregating → Completed → Failed
- Card states: disable actions for terminal states; surface failure messages from events
- Priority: allow drag to reorder within a column and optionally show a handle; provide a number input for precise changes
- Selection behavior: single selection opens details drawer; ESC to close; deep-links by task id
- Keyboard nav: arrow keys across columns/cards; Enter to open details
- Performance: windowed lists (e.g., `svelte-virtual-list`) for large task sets
- Accessibility: ARIA roles for listbox/grid, focus management on SSE refreshes

### Drag-and-Drop Semantics

- Do not allow direct status changes via DnD (status is orchestrator-controlled). Backend is the source of truth.
- Supported DnD now: reorder within a column to propose priority changes
  - On drop: update client-side order immediately (optimistic UI). Persist once a backend priority-update endpoint exists.
  - Reconciliation: on SSE updates, preserve user-defined order per-column by maintaining a stable ordering map layered over backend-sorted lists.
- Future: allow cross-column DnD to request status change once explicit transition APIs exist.
- Quick-action columns (optional future): a side “Actions” lane where dropping a card triggers Retry/Cancel/Force actions.

### State and SSE Handling

- Store shape: `{ byId: Record<Guid, Task>, allIds: Guid[], byStatus: Record<TaskStatus, Guid[]> }`
- SSE merges: upsert task by id and recompute status groupings; debounce renders
- Event log: per-selected-task log kept in memory; bounded length; lazy fetch from `/admin/events` on demand

### Theming and Layout

- Light theme default with clear status colors; support CSS variables for easy tweaks
- Responsive: columns become horizontally scrollable on small screens; details panel collapses to modal
- Dark theme optional later via CSS vars

### Testing Approach

- Unit: stores and API client with mocked fetch
- Component: Vitest + Testing Library for `TaskCard`, `KanbanColumn`
- E2E: Playwright for core flows (submit task, see card move columns via SSE, open details, download report)
- Contract: Type tests for `TaskStatus` string values to prevent mismatches

### Phased Implementation Plan

Phase 1: SvelteKit scaffolding

- Tooling: SvelteKit + TypeScript + ESLint + Prettier + Tailwind
- Configure `@sveltejs/adapter-static` for static export
- Dev proxy for `/api/*`; build outputs copied to `ResearchAgentNetwork.Web/wwwroot`
- Simple landing with tasks list (no Kanban yet)

Phase 2: Kanban board (read-only)

- `KanbanBoard` and `KanbanColumn` rendering from `/api/tasks`
- Filters and basic card layout; open details, fetch report

Phase 3: SSE live updates

- Subscribe to `/api/events`; update board reactively
- Visual indicators for updates (pulse highlight on changed cards)

Phase 4: Actions and settings

- Card actions (Retry/Cancel/Force)
- Settings panel integration

Phase 5: Reports and admin

- Markdown render and download
- Admin tab for `/admin/tasks` and `/admin/events`

Phase 6: DnD reordering (priority UX)

- Client-side reorder within columns without changing state; persist once backend priority-update endpoint is available

Each phase should be a separate PR (≤10 files, ≤300 LOC where feasible) with its own tests.

### Dev and Build Setup (proposed)

- Create `ui/` directory for SvelteKit app
- Dev: run ASP.NET backend on 5000, SvelteKit dev on 5173 with proxy:
  - SvelteKit proxy: `/api`, `/admin` → `http://localhost:5000`
- Build: `pnpm build` (SvelteKit with adapter-static) outputs to `ui/build`; copy to `ResearchAgentNetwork.Web/wwwroot`
- Optional CI step/script to clean and sync assets to `wwwroot`

### Side-by-Side Rollout and Comparison

Option 1: Single backend, two UIs, different base paths (recommended)

- Keep `ResearchAgentNetwork.Web` as the single ASP.NET backend and legacy UI at `/`
- Configure SvelteKit `paths.base = '/v2'` and export statically
- Deploy SvelteKit build under `ResearchAgentNetwork.Web/wwwroot/v2/`
- Result: legacy UI at `/`, new UI at `/v2` using the same APIs and SSE
- Pros: no duplication of backend; easy to compare; simple hosting
- Cons: Need to set SvelteKit base path; some absolute links need care

Option 2: Separate static host project for the new UI

- Create `ResearchAgentNetwork.Web.UIv2` (ASP.NET minimal project) serving only static files from its `wwwroot`
- Point the new UI at the original API host via absolute URLs/proxy
- Pros: hard separation for experiments; independent app pool/process
- Cons: extra project to maintain; CORS/proxy config; duplicated hosting plumbing

Folder structure proposal:

- `ui/` (SvelteKit project)
  - `src/` SvelteKit app
  - `static/` public assets
  - Builds to `ui/build`
- Deploy:
  - Option 1: copy `ui/build` → `ResearchAgentNetwork.Web/wwwroot/v2`
  - Option 2: copy `ui/build` → `ResearchAgentNetwork.Web.UIv2/wwwroot`

### Risks and Mitigations

- SSE scalability: keep per-client minimal processing; debounce store updates
- Large task lists: virtualized rendering
- Status drift: prevent user edits to status; actions only via API
- Auth (future): if needed, add headers and CSRF protections; for now, local app

### Open Questions

- Any SSR requirements that would push us to a non-static adapter later?
- Do we want swimlanes (by parent/priority) in v1 or later?

### Framework Choice Rationale

Why SvelteKit (+ Tailwind) fits this project now:

- Static export fits our single-backend hosting: `adapter-static` drops into `ResearchAgentNetwork.Web/wwwroot` with zero infra changes
- Lightweight runtime and straightforward reactivity reduce complexity for SSE-driven Kanban and real-time updates
- Faster delivery velocity vs. React/Next due to less boilerplate and simpler state management
- Smooth Tailwind integration for consistent theming and smaller component code
- Clear migration path: we can switch adapters later to enable SSR if needed without a ground-up rewrite

Alternatives considered:

- Next.js (React): strongest ecosystem and hiring pool; larger bundles and more boilerplate for our use case
- Nuxt (Vue): good DX and ecosystem; slightly heavier runtime and templating overhead vs Svelte
- SolidStart: great performance; ecosystem smaller and fewer off-the-shelf UI/DnD libs
- Astro (islands): excellent for content-first sites; adds complexity for app-like, SSE-heavy Kanban
- Blazor: tight .NET alignment; less mature ecosystem for Kanban/DnD and often heavier client model
- Qwik: extreme perf/resumability; unnecessary complexity for our requirements

When to revisit the choice:

- We need React-only libraries or enterprise UI kits that significantly cut build time
- SEO/SSR requirements outgrow static export and we want server adapters or edge rendering
- Team scale or contributor base strongly prefers React, changing maintenance trade-offs

Decision: proceed with SvelteKit + Tailwind and `adapter-static` now; reassess if any of the above triggers occur.

### Decisions (2025-08-11)

- Framework: SvelteKit with `adapter-static`
- Styling: Tailwind CSS
- DnD: enable client-side reorder within a column now; do not change status; backend remains source of truth; later we can add status-change via DnD with explicit APIs
- Reports: Markdown-only initially
- No feature flag for UI—phase rollout via PRs
