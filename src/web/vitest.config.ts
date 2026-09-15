import { defineConfig } from 'vitest/config';

// Pruebas unitarias de logica pura: sin navegador ni DOM, y sin dependencias de
// prueba adicionales (menos superficie de cadena de suministro). Las de punta a
// punta corren con Playwright contra la aplicacion real, en e2e/.
export default defineConfig({
  test: {
    include: ['src/**/*.test.ts'],
    environment: 'node',
  },
});
