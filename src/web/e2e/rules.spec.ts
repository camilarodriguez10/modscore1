import { expect, test, type Page } from '@playwright/test';
import { download, signIn } from './support';

/**
 * Administracion de reglas de punta a punta: editar, simular, comparar el borrador con una
 * aplicacion, publicar, ver el aviso y descargar la comparacion.
 *
 * Con el almacen en memoria del adaptador Dummy lo publicado vive mientras corra la capa
 * System. La prueba no supone la version vigente: mueve un punto de Kenna a MFA sobre lo
 * que haya, y lee los identificadores de la pantalla.
 */

function metricWeight(page: Page, name: string) {
  return page
    .getByRole('listitem')
    .filter({ has: page.getByText(name, { exact: true }) })
    .getByLabel('Weight');
}

test.describe.configure({ mode: 'serial' });

test('an administrator prepares a draft, sees its effect on one application and publishes it', async ({ page }) => {
  await signIn(page);

  await page.getByRole('link', { name: 'Rules', exact: true }).click();
  await expect(page.getByRole('heading', { level: 1 })).toContainText('Rules v');

  const kenna = metricWeight(page, 'Kenna Score');
  const mfa = metricWeight(page, 'MFA');
  const kennaBefore = Number(await kenna.inputValue());
  const mfaBefore = Number(await mfa.inputValue());

  await kenna.fill(String(kennaBefore - 1));
  await mfa.fill(String(mfaBefore + 1));
  await expect(page.getByText('2 changes')).toBeVisible();

  await page.getByRole('button', { name: 'Simulate impact' }).click();
  await expect(page.getByRole('heading', { name: 'Impact on the portfolio' })).toBeVisible();

  // Sugerencia 5: el borrador comparado con una aplicacion, metrica por metrica.
  await page.getByRole('link', { name: 'Compare your draft' }).click();
  await expect(page.getByRole('heading', { name: 'Compare rules' })).toBeVisible();
  await expect(page.getByLabel('To')).toHaveValue('draft');

  await page.getByLabel('Application (EAI code)').fill('14381');
  await page.getByRole('button', { name: 'Show application' }).click();
  await page.getByRole('button', { name: 'Simulate the draft' }).click();

  await expect(page.getByRole('heading', { name: /^14381 · / })).toBeVisible();
  const kennaRow = page.getByRole('row').filter({ has: page.getByText('Kenna Score', { exact: true }) });
  await expect(kennaRow).toContainText(`${kennaBefore} → ${kennaBefore - 1}`);

  // Publicar: el borrador sobrevivio a la navegacion.
  await page.getByRole('link', { name: 'Back to the rules editor' }).click();
  await expect(page.getByText('2 changes')).toBeVisible();
  await page.getByRole('button', { name: 'Simulate impact' }).click();
  await page.getByLabel('Reason for the change').fill('End-to-end test: one point from Kenna to MFA');
  await page.getByRole('button', { name: 'Publish as a new version' }).click();

  const published = page.getByText(/^Published rules v\d+\.\d+$/);
  await expect(published).toBeVisible();
  const version = (await published.textContent())!.replace('Published rules ', '');

  // Sugerencia 7: quien consulta se entera de las reglas nuevas.
  await expect(page.getByText(`Scoring rules changed to ${version}`)).toBeVisible();
  await page.getByRole('button', { name: 'Dismiss' }).click();
  await expect(page.getByText(`Scoring rules changed to ${version}`)).toBeHidden();
});

test('an administrator compares two published versions and downloads the comparison', async ({ page }) => {
  await signIn(page, '/admin/rules/compare');
  await expect(page.getByRole('heading', { name: 'Compare rules' })).toBeVisible();

  await page.getByLabel('From').selectOption('v1.3');
  await expect(page.getByRole('heading', { name: 'What changed' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Portfolio impact' })).toBeVisible();

  const to = await page.getByLabel('To').inputValue();
  const { file, rows } = await download(page, () => page.getByRole('link', { name: 'Download CSV' }).click());

  expect(file.suggestedFilename()).toMatch(new RegExp(`^modscore-rules-v1\\.3-to-${to.replace('.', '\\.')}-\\d{8}-\\d{4}\\.csv$`));
  expect(rows[0]).toBe(`EAI Code,Application,Score v1.3,Score ${to},Change,Band v1.3,Band ${to}`);
  expect(rows.length).toBeGreaterThan(200);
});
