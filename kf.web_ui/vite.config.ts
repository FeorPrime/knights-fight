import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    // `import.meta.dirname` rather than `__dirname` — Vite 8's native config
    // loader does not provide CommonJS globals.
    alias: { '@': `${import.meta.dirname}/src` },
  },
  server: {
    // The development HTTP API listens on port 5000.
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
        // Server-Sent Events must not be buffered by the dev proxy.
        configure: (proxy) => {
          proxy.on('proxyRes', (proxyRes) => {
            proxyRes.headers['x-accel-buffering'] = 'no';
          });
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    include: ['src/**/*.test.{ts,tsx}'],
  },
});
