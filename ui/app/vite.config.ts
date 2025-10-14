import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';
import tailwind from '@tailwindcss/vite';

export default defineConfig({
  plugins: [tailwind(), sveltekit()],
  resolve: {
    alias: {
      'svelte/compiler': 'svelte/compiler/index.js'
    }
  },
  server: {
    proxy: {
      '/api': { target: 'http://localhost:64932', changeOrigin: true },
      '/admin': { target: 'http://localhost:64932', changeOrigin: true }
    }
  }
});
