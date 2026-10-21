import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * Las definiciones de las seis preguntas (ADR 0025): el diccionario que cualquiera lee y
 * la pestana donde el administrador las cambia. Aqui se simula sin publicar: publicar
 * lo cubren las pruebas de API, y asi las demas pruebas ven las definiciones de fabrica.
 */
test.describe('definitions', () => {
  test('the dictionary explains every question with its values, owner and data coverage', async ({ page }) => {
    await signIn(page, '/definitions');

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('How the six questions are answered');

    for (const title of [
      'Indicators and Currency Score',
      '1 · How healthy is the portfolio?',
      '2 · Which applications should be modernized first?',
      '6 · Where are the resilience gaps?',
    ]) {
      await expect(page.getByRole('heading', { name: title })).toBeVisible();
    }

    const currency = page.getByRole('region', { name: 'Indicators and Currency Score' });
    await expect(currency.getByRole('row', { name: /Legacy platform below/ })).toContainText('60');
    await expect(currency.getByText('Owner: CMT owner')).toBeVisible();
    await expect(currency.getByText(/have a Currency Score from CMT/)).toBeVisible();
  });

  test('an administrator changes a threshold and sees how the dashboard would move', async ({ page }) => {
    await signIn(page, '/admin/rules?tab=analytics');

    const legacy = page.getByLabel('Legacy platform below');
    await legacy.fill('90');
    await expect(page.getByText('1 change', { exact: true })).toBeVisible();

    await page.getByRole('button', { name: 'Simulate impact' }).click();
    await expect(page.getByRole('heading', { name: 'Impact on the portfolio' })).toBeVisible();
    await expect(page.getByText('Legacy platform below: 60 → 90')).toBeVisible();

    const indicators = page.getByRole('table', { name: /Dashboard indicators/ });
    await expect(indicators.getByRole('row', { name: /Currency exposure/ })).toBeVisible();

    await page.getByRole('button', { name: 'Discard draft' }).click();
    await expect(legacy).toHaveValue('60');
  });

  test('levels out of order are explained before anything is published', async ({ page }) => {
    await signIn(page, '/admin/rules?tab=analytics');

    await page.getByLabel('High risk from').fill('90');
    await page.getByRole('button', { name: 'Simulate impact' }).click();

    await expect(page.getByText('The risk levels must go up: moderate < high < critical.')).toBeVisible();

    await page.getByRole('button', { name: 'Discard draft' }).click();
  });

  test('an open explanation has no serious accessibility violations', async ({ page }) => {
    await signIn(page, '/');
    await page.getByText('How this is calculated').first().click();

    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
    const serious = results.violations
      .filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')
      .map((violation) => `${violation.id}: ${violation.nodes.length} · ${violation.help}`);

    expect(serious).toEqual([]);
  });
});
