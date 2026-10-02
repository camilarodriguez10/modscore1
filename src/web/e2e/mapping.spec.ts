import { AxeBuilder } from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signIn } from './support';

/**
 * El mapping de columnas previo a la carga.
 *
 * Las pruebas de punta a punta corren con datos generados, donde la carga está apagada.
 * Para revisar la pantalla se simulan las tres respuestas de carga —la pantalla de Data
 * con la carga encendida, la revisión del mapping y el resultado—; lo que valida el
 * servidor lo cubren las pruebas de la API. La pestaña Mapping sí va contra la API real.
 */

const header = 'EAI_Code,Application_Name,Total Users,Users,Kenna Score';

const review = {
  files: [
    {
      fileName: 'export 0928.csv',
      canonicalFileName: 'eai_master.csv',
      source: 'EAI Master',
      description: 'Application inventory: one row per EAI.',
      problem: null,
      candidates: [],
      headers: ['EAI_Code', 'Application_Name', 'Total Users', 'Users', 'Kenna Score'],
      fields: [
        { column: 'EAI_Code', type: 'text', required: true, isKey: true, header: 'EAI_Code', match: 'name', feeds: [] },
        { column: 'Application_Name', type: 'text', required: false, isKey: false, header: 'Application_Name', match: 'name', feeds: [] },
        { column: 'User Count', type: 'integer', required: false, isKey: false, header: 'Users', match: 'alias', feeds: ['MFA'] },
        { column: 'Kenna_Score', type: 'integer', required: false, isKey: false, header: 'Kenna Score', match: 'alias', feeds: ['Kenna Score'] },
        { column: 'Raw_Tech_Score', type: 'number', required: false, isKey: false, header: null, match: 'missing', feeds: ['Raw Tech Score'] },
      ],
      unusedHeaders: ['Total Users'],
      problems: [],
    },
  ],
};

const uploadResult = {
  accepted: false,
  version: null,
  applicationCount: 0,
  files: [
    {
      fileName: 'export 0928.csv',
      canonicalFileName: 'eai_master.csv',
      source: 'EAI Master',
      bytes: 96,
      rowCount: 1,
      rejectedCount: 1,
      error: null,
      detectedColumns: [],
      candidates: [],
      rejectionReasons: [
        {
          reason: { code: 'row.missingKey', args: { column: 'EAI_Code' } },
          rows: 1,
          sampleRows: [3],
        },
      ],
      ignoredValues: [
        {
          reason: { code: 'row.invalidValue', args: { column: 'Kenna_Score', expected: 'integer', value: '712.5' } },
          rows: 1,
          sampleRows: [2],
        },
      ],
    },
  ],
  errors: [{ code: 'upload.empty', args: {} }],
  summary: { code: 'upload.rejected', args: { count: '1' } },
};

/** La pantalla de Data tal como la da el servidor, con la carga encendida. */
async function enableUploads(page: Page) {
  await page.route('**/api/ingestion', async (route) => {
    const response = await route.fetch();
    const body = (await response.json()) as Record<string, unknown>;
    await route.fulfill({ response, json: { ...body, uploadEnabled: true, disabledReason: null } });
  });
}

async function expectNoSeriousViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  const serious = results.violations
    .filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')
    .map((violation) => `${violation.id}: ${violation.nodes.length} · ${violation.help}`);

  expect(serious).toEqual([]);
}

for (const scheme of ['light', 'dark'] as const) {
  test.describe(`column mapping in ${scheme} mode`, () => {
    test.use({ colorScheme: scheme });

    test('reviews the mapping with only the header, then uploads with it and explains the rejected rows', async ({ page }) => {
      await enableUploads(page);

      let previewBody: unknown = null;
      let uploadBody = '';

      await page.route('**/api/ingestion/mapping/preview', async (route) => {
        previewBody = route.request().postDataJSON();
        await route.fulfill({ json: review });
      });

      await page.route('**/api/ingestion/upload', async (route) => {
        uploadBody = route.request().postData() ?? '';
        await route.fulfill({ json: uploadResult });
      });

      await signIn(page, '/data');

      await page.locator('input[type=file]').setInputFiles({
        name: 'export 0928.csv',
        mimeType: 'text/csv',
        buffer: Buffer.from(`${header}\n1001,Claims,350,internal,N/A\n`),
      });

      await page.getByRole('button', { name: 'Review column mapping' }).click();

      const mapping = page.getByRole('region', { name: 'Column mapping of export 0928.csv' });
      await expect(mapping).toBeVisible();

      // Solo viajó el encabezado: ninguna fila de datos salió del navegador.
      expect(previewBody).toEqual({ files: [{ fileName: 'export 0928.csv', header }] });

      const userCount = mapping.getByRole('combobox', { name: 'Column for User Count' });
      await expect(userCount).toHaveValue('Users');
      await userCount.selectOption('Total Users');

      await expect(mapping.getByRole('row').filter({ hasText: 'User Count' })).toContainText('Your choice');
      await expect(mapping).toContainText('columns of the file that load nothing: Users');
      await expect(mapping.getByRole('row').filter({ hasText: 'Raw_Tech_Score' })).toContainText('Not in this file');

      await expectNoSeriousViolations(page);

      await page.getByRole('button', { name: 'Upload with this mapping' }).click();

      const reasons = page.getByRole('region', { name: 'Rejected rows in EAI Master' });
      await expect(reasons).toContainText("No value in the key column 'EAI_Code'.");
      await expect(reasons).toContainText('totals, subtotals');

      // Lo ilegible de un campo opcional no rechaza la fila: queda sin dato y se dice.
      const empty = page.getByRole('region', { name: 'Values loaded as empty in EAI Master' });
      await expect(empty).toContainText("'Kenna_Score' expected an integer. For example, '712.5' in row 2.");
      await expect(empty).toContainText('whole numbers');

      // La carga lleva la fuente y solo lo decidido, no lo reconocido por alias.
      expect(uploadBody).toContain('eai_master.csv');
      expect(uploadBody).toContain('{"User Count":"Total Users"}');
      expect(uploadBody).not.toContain('Kenna Score"');

      await expectNoSeriousViolations(page);
    });
  });
}

test('the Mapping tab says which file and column load each field of the database', async ({ page }) => {
  await signIn(page, '/data?tab=mapping');

  await expect(page.getByRole('heading', { name: 'Where each value of the database comes from' })).toBeVisible();

  const master = page.getByRole('table', { name: 'Column mapping of EAI Master' });
  const userCount = master.getByRole('row').filter({ hasText: 'User Count' });
  await expect(userCount).toContainText('Users');
  await expect(userCount).toContainText('whole number');

  await page.getByRole('button', { name: 'Go to Uploads to review it with your files' }).click();
  await expect(page).toHaveURL(/tab=uploads/);
});
