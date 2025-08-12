<!-- markdownlint-disable MD041 -->
## Frontend UI Setup: SvelteKit + Tailwind + Dev Proxy

This guide scaffolds a SvelteKit app under `ui/`, adds Tailwind CSS, configures static export with an optional base path `/v2` for side-by-side hosting, and sets up a dev proxy to the ASP.NET API.

### Prerequisites

- Node.js 18+ (recommend LTS)
- npm 9+ (or pnpm if preferred)
- PowerShell 7+ (for the copy script examples)

### 1) Scaffold SvelteKit

Interactive (recommended if new to Svelte):

```powershell
cd .\
mkdir ui; cd ui
npm create svelte@latest .
# Choose: Skeleton project, TypeScript, ESLint, Prettier
npm install
```

### 2) Add Tailwind CSS

```powershell
npm install -D tailwindcss postcss autoprefixer @tailwindcss/forms @tailwindcss/typography
npx tailwindcss init -p
```

Update `tailwind.config.cjs` (or `tailwind.config.js`) content to include Svelte files:

```js
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './src/**/*.{html,js,svelte,ts}'
  ],
  theme: { extend: {} },
  plugins: [require('@tailwindcss/forms'), require('@tailwindcss/typography')]
};
```

Create `src/app.css` with Tailwind directives:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;
```

Import global CSS in `src/routes/+layout.svelte`:

```svelte
<script>
  import '../app.css';
</script>

<slot />
```

### 3) Static Adapter with optional base path (for `/v2`)

```powershell
npm i -D @sveltejs/adapter-static
```

Edit `svelte.config.js` to use the static adapter and an optional base path via env var `BASE_PATH`:

```js
import adapter from '@sveltejs/adapter-static';

const basePath = process.env.BASE_PATH ?? '';

/** @type {import('@sveltejs/kit').Config} */
const config = {
  kit: {
    adapter: adapter(),
    paths: { base: basePath }
  }
};

export default config;
```

Build with a base path for side-by-side hosting at `/v2`:

```powershell
# Windows PowerShell
$env:BASE_PATH = '/v2'; npm run build
```

### 4) Dev Proxy to ASP.NET API

Edit `vite.config.ts` (or create if missing) to proxy `/api` and `/admin` to your ASP.NET host (default `http://localhost:5000`):

```ts
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [sveltekit()],
  server: {
    proxy: {
      '/api': { target: 'http://localhost:5000', changeOrigin: true },
      '/admin': { target: 'http://localhost:5000', changeOrigin: true }
    }
  }
});
```

Run dev server:

```powershell
npm run dev -- --open
```

### 5) Deploy statically under `ResearchAgentNetwork.Web/wwwroot/v2`

After building with `BASE_PATH='/v2'`, copy files to the ASP.NET app’s static root:

```powershell
# From repo root
Remove-Item -Recurse -Force .\ResearchAgentNetwork.Web\wwwroot\v2 -ErrorAction SilentlyContinue
New-Item -ItemType Directory .\ResearchAgentNetwork.Web\wwwroot\v2 | Out-Null
Copy-Item -Recurse -Force .\ui\build\* .\ResearchAgentNetwork.Web\wwwroot\v2\
```

Result:

- Legacy UI remains at `/`
- New SvelteKit UI is available at `/v2`

### 6) Minimal sanity check page

Create `src/routes/+page.svelte` to verify Tailwind and API proxy:

```svelte
<script lang="ts">
  import { onMount } from 'svelte';
  let tasks: any[] = [];
  onMount(async () => {
    const res = await fetch('/api/tasks');
    tasks = await res.json();
  });
</script>

<h1 class="text-2xl font-bold">RAN UI v2</h1>
<p class="text-sm text-gray-600">SvelteKit + Tailwind</p>

<ul class="mt-4 space-y-2">
  {#each tasks as t}
    <li class="p-3 rounded border">
      <span class="font-mono text-xs">{t.id}</span>
      <div class="font-semibold">{t.description}</div>
      <div class="text-xs">{t.status}</div>
    </li>
  {/each}
</ul>
```

### 7) Libraries for Kanban and Markdown

- DnD: `npm i svelte-dnd-action`
- Markdown: `npm i marked`

### 8) Building with and without base path

- Local dev: no base path needed → `npm run dev`
- Static side-by-side under `/v2`: build with base → `$env:BASE_PATH = '/v2'; npm run build`
- Static replace legacy at root later: build without base → `npm run build` and copy to `wwwroot` (not `wwwroot/v2`)

### 9) Next steps

- Implement `KanbanBoard` and `TaskCard` components; wire SSE via `/api/events`
- Add settings and actions; integrate Markdown report view
- Introduce client-side DnD reorder within columns (no status change)

### 10) Git ignore rules for UI

The repo’s root `.gitignore` was updated to exclude SvelteKit/Vite development artifacts and deployment outputs:

- `**/node_modules/`, `**/.vite/`, `**/node_modules/.vite/`
- `**/.svelte-kit/` (SvelteKit temp build)
- `ui/app/build/` (static export output used by the copy step)
- `ui/app/.env`, `ui/app/.env.*` (UI-specific environment files)
- `ui/app/.vercel/`, `ui/app/.netlify/` (platform adapters when experimenting)
- `ResearchAgentNetwork.Web/wwwroot/v*/` (versioned static deployments, e.g. `/v2`)
- `ResearchAgentNetwork.Web/wwwroot/v*/` (versioned static deployments, e.g. `/v2`)
- `ResearchAgentNetwork.Web/wwwroot/_app/` (when deploying to root without a base path)

This keeps the repo clean while allowing side-by-side static deployments to `wwwroot/<version>` using `build-ui.ps1`.

Lockfiles: we keep `package-lock.json` files committed to ensure reproducible builds.

### Appendix: Build script

You can use a helper script to build and deploy the UI to `wwwroot/<base>` in one step:

```powershell
pwsh ./build-ui.ps1                 # builds with BasePath /v2 and copies to wwwroot/v2
pwsh ./build-ui.ps1 -BasePath /v3   # builds with BasePath /v3
pwsh ./build-ui.ps1 -SkipInstall    # skips npm install step
```
