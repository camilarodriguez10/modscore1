import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';

// El dev server solo habla con el channel-api. Nunca con la capa System:
// esa frontera es la misma en desarrollo que en despliegue (ADR 0001).
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5100',
        changeOrigin: false,
      },
    },
  },
  build: {
    // Sin source maps en produccion: publicarlos entrega el codigo fuente completo.
    sourcemap: false,
    target: 'es2022',
  },
});
