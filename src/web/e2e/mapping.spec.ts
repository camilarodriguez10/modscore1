import { AxeBuilder } from '@axe-core/playwright';
import { deflateRawSync } from 'node:zlib';
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

    test('leaves out the files that failed and recalculates with the rest, after confirming', async ({ page }) => {
      await enableUploads(page);

      const verdict = (fileName: string, canonicalFileName: string | null, source: string, error: unknown = null) => ({
        fileName,
        canonicalFileName,
        source,
        bytes: 64,
        rowCount: error ? 0 : 3,
        rejectedCount: 0,
        error,
        detectedColumns: [],
        candidates: [],
        rejectionReasons: [],
        ignoredValues: [],
      });

      const bodies: string[] = [];

      await page.route('**/api/ingestion/upload', async (route) => {
        bodies.push(route.request().postData() ?? '');

        await route.fulfill({
          json:
            bodies.length === 1
              ? {
                  accepted: false,
                  version: null,
                  applicationCount: 0,
                  files: [
                    verdict('containers.csv', null, '-', { code: 'match.unrecognized', args: {} }),
                    verdict('eai.csv', 'eai_master.csv', 'EAI Master'),
                    verdict('sca.csv', 'veracode_sca_open_flaws.csv', 'SCA Flaws', {
                      code: 'csv.fieldTooLong',
                      args: { max: '65536', row: '527', column: 'SUM_DSCR' },
                    }),
                  ],
                  errors: [{ code: 'match.unrecognized', args: { file: 'containers.csv' } }],
                  summary: { code: 'upload.rejected', args: { count: '2' } },
                }
              : {
                  accepted: true,
                  version: { id: '20261005-1300', label: 'Upload 1005', capturedAt: '2026-10-05T13:00:00Z', applicationCount: 3, isCurrent: true },
                  applicationCount: 3,
                  files: [verdict('eai.csv', 'eai_master.csv', 'EAI Master')],
                  errors: [],
                  summary: { code: 'upload.published', args: { count: '3' } },
                },
        });
      });

      await signIn(page, '/data');

      await page.locator('input[type=file]').setInputFiles([
        { name: 'containers.csv', mimeType: 'text/csv', buffer: Buffer.from('CLUSTER,NAMESPACE\nocp1,claims\n') },
        { name: 'eai.csv', mimeType: 'text/csv', buffer: Buffer.from(`${header}\n1001,Claims,350,internal,10\n`) },
        { name: 'sca.csv', mimeType: 'text/csv', buffer: Buffer.from('FLAW_IDX_NUM,EAI_CD,SVR_DSCR,FND_STTS_DSCR\n1,1001,1 - Critical,Open\n') },
      ]);

      await page.getByRole('button', { name: 'Upload and recalculate' }).click();

      await expect(page.getByText('Upload rejected', { exact: true })).toBeVisible();
      await expect(page.getByText('The SUM_DSCR cell in row 527', { exact: false }).first()).toBeVisible();

      const leaveOut = page.locator('section', {
        has: page.getByRole('heading', { name: 'Recalculate with the files that passed' }),
      });
      await expect(leaveOut).toContainText('SCA Flaws will have no data in this upload');
      await expect(leaveOut).toContainText('It is not one of the sources of the model');

      await expectNoSeriousViolations(page);

      // Nada se publica hasta confirmar.
      expect(bodies).toHaveLength(1);

      await leaveOut.getByRole('button', { name: 'Leave out 2 files and recalculate' }).click();

      await expect(page.getByText('Upload published', { exact: true })).toBeVisible();

      expect(bodies).toHaveLength(2);
      expect(bodies[1]).toContain('filename="eai.csv"');
      expect(bodies[1]).not.toContain('containers.csv');
      expect(bodies[1]).not.toContain('sca.csv');
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

/** Un libro de Excel mínimo, armado como lo escribe Excel: partes comprimidas en un ZIP. */
function workbook(rows: string[][]): Buffer {
  const main = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
  const rels = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';
  const pkg = 'http://schemas.openxmlformats.org/package/2006/relationships';
  const cells = (row: string[], r: number) =>
    row.map((value, c) => `<c r="${String.fromCharCode(65 + c)}${r}" t="inlineStr"><is><t>${value}</t></is></c>`).join('');

  const parts: Record<string, string> = {
    '_rels/.rels': `<Relationships xmlns="${pkg}"><Relationship Id="r1" Type="${rels}/officeDocument" Target="xl/workbook.xml"/></Relationships>`,
    'xl/workbook.xml': `<workbook xmlns="${main}" xmlns:r="${rels}"><sheets><sheet name="US" sheetId="1" r:id="s1"/></sheets></workbook>`,
    'xl/_rels/workbook.xml.rels': `<Relationships xmlns="${pkg}"><Relationship Id="s1" Type="${rels}/worksheet" Target="worksheets/sheet1.xml"/></Relationships>`,
    'xl/worksheets/sheet1.xml': `<worksheet xmlns="${main}"><sheetData>${rows.map((row, i) => `<row r="${i + 1}">${cells(row, i + 1)}</row>`).join('')}</sheetData></worksheet>`,
  };

  const locals: Buffer[] = [];
  const directory: Buffer[] = [];
  let offset = 0;

  for (const [name, content] of Object.entries(parts)) {
    const fileName = Buffer.from(name);
    const raw = Buffer.from(content);
    const data = deflateRawSync(raw);

    const local = Buffer.alloc(30);
    local.writeUInt32LE(0x04034b50, 0);
    local.writeUInt16LE(8, 8);
    local.writeUInt32LE(data.length, 18);
    local.writeUInt32LE(raw.length, 22);
    local.writeUInt16LE(fileName.length, 26);

    const entry = Buffer.alloc(46);
    entry.writeUInt32LE(0x02014b50, 0);
    entry.writeUInt16LE(8, 10);
    entry.writeUInt32LE(data.length, 20);
    entry.writeUInt32LE(raw.length, 24);
    entry.writeUInt16LE(fileName.length, 28);
    entry.writeUInt32LE(offset, 42);

    locals.push(local, fileName, data);
    directory.push(entry, fileName);
    offset += 30 + fileName.length + data.length;
  }

  const size = directory.reduce((sum, part) => sum + part.length, 0);
  const end = Buffer.alloc(22);
  end.writeUInt32LE(0x06054b50, 0);
  end.writeUInt16LE(Object.keys(parts).length, 8);
  end.writeUInt16LE(Object.keys(parts).length, 10);
  end.writeUInt32LE(size, 12);
  end.writeUInt32LE(offset, 16);

  return Buffer.concat([...locals, ...directory, end]);
}

test('an Excel workbook sends only its header, and the files of one source are reported together', async ({ page }) => {
  await enableUploads(page);

  const apimHeader = 'EAI,Country,Application Short Name,Application Name';
  let previewBody: { files: { fileName: string; header: string }[] } | null = null;

  await page.route('**/api/ingestion/mapping/preview', async (route) => {
    previewBody = route.request().postDataJSON() as typeof previewBody;
    await route.fulfill({ json: { files: [] } });
  });

  const apim = (fileName: string, error: { code: string; args: Record<string, string> } | null = null) => ({
    fileName,
    canonicalFileName: 'apim_onboarded_eais.csv',
    source: 'APIM Onboarding',
    bytes: 900,
    rowCount: error ? 0 : 14,
    rejectedCount: 0,
    error,
    detectedColumns: [],
    candidates: [],
    rejectionReasons: [],
    ignoredValues: [],
  });

  await page.route('**/api/ingestion/upload', async (route) => {
    await route.fulfill({
      json: {
        accepted: false,
        version: null,
        applicationCount: 0,
        files: [
          { ...apim('master.csv'), canonicalFileName: 'eai_master.csv', source: 'EAI Master' },
          apim('APIM_Onboarded_EAIs_EMEA.csv'),
          apim('APIM_Onboarded_EAIs_US.xlsx', { code: 'csv.fieldTooLong', args: { max: '65536', row: '12' } }),
        ],
        errors: [{ code: 'csv.fieldTooLong', args: { max: '65536', row: '12', file: 'APIM_Onboarded_EAIs_US.xlsx' } }],
        summary: { code: 'upload.rejected', args: { count: '1' } },
      },
    });
  });

  await signIn(page, '/data');

  await page.locator('input[type=file]').setInputFiles([
    { name: 'master.csv', mimeType: 'text/csv', buffer: Buffer.from('EAI_Code,Application_Name\n1001,Claims\n') },
    { name: 'APIM_Onboarded_EAIs_EMEA.csv', mimeType: 'text/csv', buffer: Buffer.from(`${apimHeader}\n1001,Spain,CL,Claims\n`) },
    {
      name: 'APIM_Onboarded_EAIs_US.xlsx',
      mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      buffer: workbook([apimHeader.split(','), ['2002', 'USA', 'SECRET', 'Do not send this row']]),
    },
  ]);

  await page.getByRole('button', { name: 'Review column mapping' }).click();
  await expect.poll(() => previewBody).not.toBeNull();

  // Del libro viajó solo su encabezado, leído en el navegador.
  expect(previewBody!.files[2]).toEqual({ fileName: 'APIM_Onboarded_EAIs_US.xlsx', header: apimHeader });
  expect(JSON.stringify(previewBody)).not.toContain('SECRET');

  await page.getByRole('button', { name: 'Upload and recalculate' }).click();

  const detail = page.getByRole('table').filter({ hasText: 'Recognized source' });
  await expect(detail.getByRole('row').filter({ hasText: 'APIM_Onboarded_EAIs_EMEA.csv' })).toContainText('file 1 of 2');
  await expect(detail.getByRole('row').filter({ hasText: 'APIM_Onboarded_EAIs_US.xlsx' })).toContainText('file 2 of 2');

  await expect(page.getByText('APIM Onboarding is still loaded from its other files; only the rows of this one are left out.')).toBeVisible();
});
