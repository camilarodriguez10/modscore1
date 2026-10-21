import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * La navegación del 2026-10-21: Insights ya no existe y Code y Roadmap se apagan desde la
 * capa Channel. La suite corre con las dos encendidas; aquí se simula la sesión que da la
 * configuración por defecto, con las dos apagadas.
 */
test.describe('navigation', () => {
  test('with Code and Roadmap turned off, their tabs are not offered and their links go to the dashboard', async ({ page }) => {
    await page.route('**/api/session', async (route) => {
      const response = await route.fetch();
      // Antes de iniciar sesión responde 401 sin cuerpo: pasa tal cual.
      if (!response.ok()) {
        await route.fulfill({ response });
        return;
      }

      const body = (await response.json()) as Record<string, unknown>;
      await route.fulfill({ response, json: { ...body, features: { code: false, portfolioRoadmap: false } } });
    });

    await signIn(page, '/');

    const nav = page.getByRole('banner').getByRole('navigation');
    await expect(nav.getByRole('link', { name: 'Dashboard', exact: true })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Data', exact: true })).toBeVisible();

    for (const hidden of ['Insights', 'Code', 'Roadmap']) {
      await expect(nav.getByRole('link', { name: hidden, exact: true })).toHaveCount(0);
    }

    for (const path of ['/code', '/roadmap', '/insights']) {
      await page.goto(path);
      await expect(page).toHaveURL(/\/$/);
      await expect(page.getByRole('heading', { name: 'Portfolio Health & Risk Intelligence' })).toBeVisible();
    }
  });

  test('a saved link to the retired Insights screen opens the dashboard with its filter', async ({ page }) => {
    await signIn(page, '/insights?country=Chile');

    await expect(page).toHaveURL(/\/\?country=Chile$/);
    await expect(page.getByRole('heading', { name: 'Portfolio Health & Risk Intelligence' })).toBeVisible();
  });
});
