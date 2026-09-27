import { defineConfig } from '@playwright/test';

/**
 * Pruebas de punta a punta contra la aplicacion real: capa System, capa Channel y la SPA.
 *
 * Por defecto usa el Microsoft Edge instalado en el equipo (`channel: 'msedge'`): no
 * descarga navegadores. En el CI, `MODSCORE_E2E_CHANNEL=chromium` usa el Chromium que
 * instala Playwright, que es lo que hay en un runner de Linux.
 *
 * Levanta las tres piezas con datos dummy y autenticacion de desarrollo, o reutiliza las
 * que ya esten corriendo. Para probar un sitio ya arrancado —por ejemplo, la capa Channel
 * sirviendo el sitio compilado con --webroot—:
 *
 *   MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test
 */

const externalBaseUrl = process.env.MODSCORE_E2E_BASE_URL;
const channel = process.env.MODSCORE_E2E_CHANNEL ?? 'msedge';
const reuse = !process.env.CI;

// Los limites por usuario asumen personas, no una suite que navega sin pausa.
const generousLimits = {
  RateLimiting__RequestsPerMinute: '100000',
  RateLimiting__SimulationsPerMinute: '10000',
  RateLimiting__RulesSimulationsPerMinute: '10000',
  RateLimiting__ExportsPerMinute: '10000',
};

export default defineConfig({
  testDir: './e2e',
  // Las pruebas de reglas publican sobre el mismo almacen en memoria: en orden.
  workers: 1,
  fullyParallel: false,
  retries: process.env.CI ? 1 : 0,
  timeout: 60_000,
  reporter: [['list']],
  use: {
    baseURL: externalBaseUrl ?? 'http://localhost:5173',
    channel,
    acceptDownloads: true,
    trace: 'retain-on-failure',
  },
  ...(externalBaseUrl
    ? {}
    : {
        webServer: [
          {
            command: 'dotnet run --project ../system/Modernization.SystemApi --no-launch-profile',
            url: 'http://localhost:5101/health',
            reuseExistingServer: reuse,
            timeout: 240_000,
            env: {
              ASPNETCORE_ENVIRONMENT: 'Development',
              ASPNETCORE_URLS: 'http://localhost:5101',
              DataSource__Provider: 'Dummy',
            },
          },
          {
            command: 'dotnet run --project ../channel/Modernization.ChannelApi --no-launch-profile',
            url: 'http://localhost:5100/health',
            reuseExistingServer: reuse,
            timeout: 240_000,
            env: {
              ASPNETCORE_ENVIRONMENT: 'Development',
              ASPNETCORE_URLS: 'http://localhost:5100',
              ...generousLimits,
            },
          },
          {
            command: 'npm run dev -- --strictPort',
            url: 'http://localhost:5173',
            reuseExistingServer: reuse,
            timeout: 60_000,
          },
        ],
      }),
});
