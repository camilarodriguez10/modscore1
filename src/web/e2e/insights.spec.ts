import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * Pantalla 09 — las seis preguntas del portafolio. Se comprueba que estén las seis,
 * que el filtro de la barra las acote, y que los dos controles propios de la pantalla
 * —el umbral de duplicados y la agrupación del riesgo— viajen en la URL como el resto.
 */
const QUESTIONS = [
  'How healthy is the portfolio?',
  'Which applications should be modernized first?',
  'Where should we reduce complexity?',
  'Where is technology risk concentrated?',
  'What is driving obsolescence?',
  'Where are the resilience gaps?',
];

test.describe('insights', () => {
  test('the six questions are answered on one screen', async ({ page }) => {
    await signIn(page, '/insights');

    await expect(page.getByRole('heading', { level: 1 })).toContainText('Six questions about');

    for (const question of QUESTIONS) {
      await expect(page.getByRole('heading', { name: question })).toBeVisible();
    }
  });

  test('the answers follow the filter on screen', async ({ page }) => {
    await signIn(page, '/insights?country=Mexico');

    await expect(page.getByRole('heading', { level: 1 })).toContainText('Six questions about');

    // El filtro sigue puesto al cambiar de pantalla, y vuelve con el mismo recorte.
    await page.getByRole('link', { name: 'Portfolio', exact: true }).click();
    await expect(page).toHaveURL(/country=Mexico/);

    await page.getByRole('link', { name: 'Insights', exact: true }).click();
    await expect(page).toHaveURL(/\/insights\?.*country=Mexico/);
  });

  test('the duplicate threshold and the risk grouping travel in the URL', async ({ page }) => {
    await signIn(page, '/insights');

    await page.getByRole('button', { name: '5', exact: true }).click();
    await expect(page).toHaveURL(/duplicates=5/);

    await page.getByRole('button', { name: 'Country', exact: true }).click();
    await expect(page).toHaveURL(/riskBy=country/);
    await expect(page).toHaveURL(/duplicates=5/);
  });

  test('a risk group becomes a filter for the whole site', async ({ page }) => {
    await signIn(page, '/insights?riskBy=country');

    const group = page.getByRole('button', { name: 'Mexico', exact: true });
    await expect(group).toBeVisible();
    await group.click();

    await expect(page).toHaveURL(/country=Mexico/);

    await page.getByRole('link', { name: 'Roadmap', exact: true }).click();
    await expect(page).toHaveURL(/\/roadmap\?.*country=Mexico/);
  });
});
