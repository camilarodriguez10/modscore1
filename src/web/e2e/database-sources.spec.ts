import { AxeBuilder } from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signIn } from './support';

/**
 * Las fuentes leídas de bases de datos (ADR 0037).
 *
 * Las pruebas de punta a punta corren con datos generados y sin conexiones configuradas. Se
 * simulan las respuestas —la pantalla de Data con conectores, la prueba de conexiones y la
 * lectura—; lo que valida el servidor (TLS, consultas, publicación) lo cubren las pruebas
 * de la API y de los conectores.
 */

const connectors = {
  pullEnabled: true,
  disabledReason: null,
  pullIntervalMinutes: null,
  connections: [
    { name: 'cmt-sql', provider: 'SqlServer' },
    { name: 'sca-pg', provider: 'PostgreSql' },
  ],
  sources: [
    { source: 'EAI Master', fileName: 'eai_master.csv', connection: 'cmt-sql', kind: 'table' },
    { source: 'Tech Debt', fileName: 'tech_debt.csv', connection: 'cmt-sql', kind: 'query' },
    { source: 'SCA Flaws', fileName: 'veracode_sca_open_flaws.csv', connection: 'sca-pg', kind: 'table' },
  ],
};

const failedPull = {
  upload: {
    accepted: false,
    version: null,
    applicationCount: 0,
    files: [],
    // La capa Channel saca de la lista el aviso que pasa a ser el resumen.
    errors: [],
    summary: { code: 'connector.pullFailed', args: { count: '1', total: '3' } },
  },
  sources: [
    { source: 'EAI Master', fileName: 'eai_master.csv', connection: 'cmt-sql', rows: 250, bytes: 48000, elapsedMilliseconds: 820, error: null },
    { source: 'Tech Debt', fileName: 'tech_debt.csv', connection: 'cmt-sql', rows: 240, bytes: 9000, elapsedMilliseconds: 410, error: null },
    {
      source: 'SCA Flaws',
      fileName: 'veracode_sca_open_flaws.csv',
      connection: 'sca-pg',
      rows: 0,
      bytes: 0,
      elapsedMilliseconds: 30000,
      error: { code: 'connector.timeout', args: { connection: 'sca-pg', source: 'veracode_sca_open_flaws' } },
    },
  ],
};

/** La pantalla de Data tal como la da el servidor, con fuentes leídas de bases. */
async function withConnectors(page: Page) {
  await page.route('**/api/ingestion', async (route) => {
    const response = await route.fetch();
    const body = (await response.json()) as Record<string, unknown>;
    await route.fulfill({ response, json: { ...body, connectors } });
  });
}

for (const scheme of ['light', 'dark'] as const) {
  test.describe(`database sources in ${scheme} mode`, () => {
    test.use({ colorScheme: scheme });

    test('lists where each source comes from, tests the connections and explains a pull that published nothing', async ({ page }) => {
      await withConnectors(page);

      let pullBody: unknown = null;

      await page.route('**/api/ingestion/connectors/test', async (route) => {
        await route.fulfill({
          json: [
            { name: 'cmt-sql', provider: 'SqlServer', ok: true, error: null, elapsedMilliseconds: 240 },
            {
              name: 'sca-pg',
              provider: 'PostgreSql',
              ok: false,
              error: { code: 'connector.tlsRequired', args: { connection: 'sca-pg', provider: 'PostgreSql' } },
              elapsedMilliseconds: 3,
            },
          ],
        });
      });

      await page.route('**/api/ingestion/pull', async (route) => {
        pullBody = route.request().postDataJSON();
        await route.fulfill({ json: failedPull });
      });

      await signIn(page, '/data');

      const card = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Database sources' }) });
      await expect(page.getByRole('heading', { name: 'Database sources' })).toBeVisible();
      await expect(card.getByRole('row').filter({ hasText: 'Tech Debt' })).toContainText('Query');
      await expect(card.getByRole('row').filter({ hasText: 'SCA Flaws' })).toContainText('PostgreSql');

      await page.getByRole('button', { name: 'Test connections' }).click();

      const results = page.getByRole('list', { name: 'Connection test results' });
      await expect(results).toContainText('connected in 0.2 s');
      await expect(results).toContainText('does not use verified TLS');

      await page.getByLabel('Upload label').last().fill('Pull 1021');
      await page.getByRole('button', { name: 'Pull from databases and recalculate' }).click();

      await expect(page.getByText('Upload rejected')).toBeVisible();
      await expect(page.getByText('1 of 3 sources could not be read, so nothing was published.')).toBeVisible();
      const detail = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Per-source detail' }) });
      await expect(detail.getByRole('row').filter({ hasText: 'SCA Flaws' })).toContainText('took longer than allowed');
      await expect(detail.getByRole('row').filter({ hasText: 'EAI Master' })).toContainText('read');

      // Solo viaja la etiqueta: las conexiones y las consultas no salen del servidor.
      expect(pullBody).toEqual({ label: 'Pull 1021' });

      const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
      const serious = axe.violations
        .filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')
        .map((violation) => `${violation.id}: ${violation.nodes.length} · ${violation.help}`);

      expect(serious).toEqual([]);
    });
  });
}
