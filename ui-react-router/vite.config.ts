import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwind from '@tailwindcss/vite';

// https://vitejs.dev/config/
export default defineConfig({
	plugins: [react(), tailwind()],
	server: {
		proxy: {
			'/api': 'http://localhost:64932',
			'/admin': 'http://localhost:64932',
		},
	},
});

