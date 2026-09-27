import { expect, test, type Page } from '@playwright/test';
import { signIn } from './support';

/** Un panel por su titulo: las tarjetas son secciones con un encabezado. */
const panel = (page: Page, title: string) =>
  page.locator('section').filter({ has: page.getByRole('heading', { name: title, exact: true }) });

/**
 * La pantalla Code (ADR 0026): obsolescencia y riesgo segun los repositorios, con datos de
 * demostracion hasta que se conecte GitHub Enterprise. Lo primero que tiene que decir es
 * de donde salen los datos.
 */
test.describe('code', () => {
  test('says the repositories are sample data and where the end-of-life dates come from', async ({ page }) => {
    await signIn(page, '/code');

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Component obsolescence & risk');

    const source = page.getByRole('region', { name: 'Data source' });
    await expect(source).toContainText('Sample repositories — GitHub Enterprise is not connected yet');
    await expect(source.getByRole('link', { name: 'endoflife.date' })).toHaveAttribute('href', 'https://endoflife.date');

    const indicators = page.getByRole('list', { name: 'Code indicators' });
    await expect(indicators.getByRole('listitem')).toHaveCount(5);
    await expect(indicators).toContainText('Repositories analyzed');
    await expect(indicators).toContainText('Past end of life');
  });

  test('a lifecycle status filters the release cycles', async ({ page }) => {
    await signIn(page, '/code');

    const lifecycle = panel(page, 'Where is the stack reaching its end of life?');
    await lifecycle.getByRole('group', { name: 'Show cycles' }).getByRole('button', { name: 'Supported', exact: true }).click();

    const statuses = lifecycle.getByRole('table').getByRole('row').locator('td:nth-child(4)');
    await expect(statuses.first()).toHaveText('Supported');
    for (const text of await statuses.allTextContents()) {
      expect(text).toBe('Supported');
    }
  });

  test('an indicator focuses the applications and the detail explains the index', async ({ page }) => {
    await signIn(page, '/code');

    await page.getByRole('list', { name: 'Code indicators' }).getByRole('button', { name: /Past end of life/ }).click();

    const applications = panel(page, 'Which applications carry the most code risk?');
    await expect(applications.getByRole('button', { name: 'Past end of life', exact: true })).toHaveAttribute('aria-pressed', 'true');

    const first = applications.getByRole('table').getByRole('row').nth(1).getByRole('button');
    const name = await first.textContent();
    await first.click();

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { level: 2 })).toHaveText(name ?? '');
    await expect(dialog.getByRole('heading', { name: 'How the index adds up' })).toBeVisible();
    // La fila del desglose (con el «×» de los puntos), no la de la proyección.
    await expect(dialog.getByRole('row', { name: /Components past end of life \d+ ×/ })).toBeVisible();
    await expect(page).toHaveURL(/app=\d+/);

    await page.keyboard.press('Escape');
    await expect(dialog).toBeHidden();
    await expect(page).not.toHaveURL(/app=/);
  });

  test('the filter bar narrows the code screen', async ({ page }) => {
    await signIn(page, '/code');
    const everything = await page.getByRole('list', { name: 'Code indicators' }).getByRole('listitem').first().textContent();

    await signIn(page, '/code?lob=Individual');
    const narrowed = page.getByRole('list', { name: 'Code indicators' }).getByRole('listitem').first();

    await expect(narrowed).not.toHaveText(everything ?? '');
    // Por el comienzo del nombre: una opcion del filtro CIO es "R. Villalobos".
    await expect(page.getByRole('combobox', { name: /^LOB/ })).toHaveValue('Individual');
  });

  test('the definitions and the rules editor carry the code parameters', async ({ page }) => {
    await signIn(page, '/definitions#code');

    const section = page.getByRole('region', { name: 'Code · Component obsolescence & risk' });
    await expect(section.getByRole('row', { name: /Ending soon within/ })).toContainText('12 months');
    await expect(section.getByText(/have repositories \(sample data until GitHub Enterprise is connected\)/)).toBeVisible();

    await signIn(page, '/admin/rules?tab=analytics');
    await expect(page.getByLabel('Ending soon within')).toHaveValue('12');
  });

  test('compares score, currency and risk across uploads and projects the next three years', async ({ page }) => {
    await signIn(page, '/code');

    const history = panel(page, 'How did score, currency and risk move across uploads?');
    // Cuatro trimestres del mismo portafolio en modo Dummy: la cabecera y cuatro filas.
    await expect(history.getByRole('table').getByRole('row')).toHaveCount(5);
    await expect(history).toContainText('Currency Score');
    await expect(history).toContainText('Risk Score');

    const outlook = panel(page, 'Where is obsolescence heading if nothing changes?');
    for (const horizon of ['Today', 'In 1 year', 'In 2 years', 'In 3 years']) {
      await expect(outlook.getByRole('region', { name: horizon })).toBeVisible();
    }

    const plan = panel(page, 'What should change first?');
    await expect(plan.getByRole('row').nth(1)).toContainText('Now');
    await expect(plan.getByText(/^(Upgrade|Migrate|Replace|Update) /).first()).toBeVisible();
  });

  test('an application shows its suggestions, its next three years and its uploads', async ({ page }) => {
    await signIn(page, '/code?app=14381');

    const dialog = page.getByRole('dialog');
    await expect(dialog.getByRole('heading', { name: 'Suggested improvements' })).toBeVisible();
    await expect(dialog.getByRole('heading', { name: 'Today and in 1, 2 and 3 years, if nothing changes' })).toBeVisible();

    const uploads = dialog.locator('section').filter({ has: page.getByRole('heading', { name: 'Across uploads' }) });
    await expect(uploads.getByRole('row')).toHaveCount(5);
    // El ejemplo del BRD no cambia entre trimestres.
    await expect(uploads.getByRole('row').nth(1)).toContainText('79.67');
  });
});
