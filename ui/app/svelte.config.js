import adapter from '@sveltejs/adapter-static';
// Removed vitePreprocess to avoid node resolving 'svelte/compiler' during config evaluation

/** @type {import('@sveltejs/kit').Config} */
const config = {
	// Consult https://svelte.dev/docs/kit/integrations
  // for more information about preprocessors
  // Svelte 5 supports TS natively; Tailwind handled via Vite plugin. No preprocess needed here.

	kit: {
		// adapter-auto only supports some environments, see https://svelte.dev/docs/kit/adapter-auto for a list.
		// If your environment is not supported, or you settled on a specific environment, switch out the adapter.
		// See https://svelte.dev/docs/kit/adapters for more information about adapters.
    adapter: adapter(),
    paths: { base: process.env.BASE_PATH ?? '' }
	}
};

export default config;
