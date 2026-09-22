import { fileURLToPath, URL } from 'node:url'

import tailwindcss from '@tailwindcss/vite'
import vue from '@vitejs/plugin-vue'
import { defineConfig } from 'vite'

/**
 * Dev proxy for the Katalog API (arch report 4.2).
 *
 * The SPA fetches relative `/api/*` paths. In dev, Vite forwards them to the
 * backend. When the app runs as an Aspire resource, Aspire injects
 * `API_HTTP`/`API_HTTPS` process env vars that name the API endpoint;
 * without them we fall back to the local API default port.
 */
function apiTarget(): string {
  return process.env.API_HTTP || process.env.API_HTTPS || 'http://localhost:5192'
}

const enableTlsInsecure = Boolean(process.env.API_HTTPS)

export default defineConfig({
  plugins: [vue(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: apiTarget(),
        changeOrigin: true,
        // Aspire dev certificates are self-signed; trust them for proxying.
        secure: !enableTlsInsecure,
      },
    },
  },
})