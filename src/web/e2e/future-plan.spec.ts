import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * El pedido del 2026-10-21: el future plan de cada aplicación llega paso por paso, y abrirlo
 * en el What-If Lab da exactamente el mismo score. «Find the minimum plan» a 90 da el mismo plan.
 */
test.describe('future plan', () => {
  test('the action plan opens in the What-If Lab with the same projected score', async ({ page }) => {
    await signIn(page, '/applications/14381/roadmap');

    await expect(page.getByRole('heading', { name: /Action plan to reach 90/ })).toBeVisible();

    const steps = page.getByRole('list', { name: 'Steps of the action plan' }).getByRole('listitem').filter({ has: page.locator('ul') });
    await expect(steps.first()).toBeVisible();
    const stepCount = await steps.count();
    expect(stepCount).toBeGreaterThan(0);

    // El score después del último paso es el del plan.
    const lastStep = (await steps.last().innerText()).match(/→ (\d+\.\d{2})/);
    expect(lastStep).not.toBeNull();
    const planScore = lastStep![1]!;

    await page.getByRole('link', { name: 'Open in What-If' }).click();
    await expect(page).toHaveURL(/\/what-if\?plan=/);
    await expect(page.getByRole('heading', { name: /./, level: 1 })).toBeVisible();

    const projection = page.getByText('With the scenario').locator('xpath=..');
    await expect(projection).toContainText(planScore);
    await expect(page.getByRole('checkbox', { checked: true })).toHaveCount(stepCount);

    // El objetivo parte en 90 y la búsqueda da el mismo plan.
    await page.getByRole('button', { name: 'Find the minimum plan' }).click();
    await page.getByRole('button', { name: 'Load this plan into the levers' }).click();
    await expect(projection).toContainText(planScore);
  });

  test('a target chosen in the What-If Lab carries over to the future plan and back', async ({ page }) => {
    // 89: por debajo del objetivo del catálogo, y la 14381 de ejemplo necesita un plan para llegar.
    await signIn(page, '/applications/14381/what-if?target=89');

    await page.getByRole('button', { name: 'Find the minimum plan' }).click();
    const steps = page.getByRole('button', { name: 'Load this plan into the levers' });
    await expect(steps).toBeVisible();
    const labSteps = await page.locator('ol li').count();

    await page.getByRole('link', { name: 'See the detailed plan in Future plan' }).click();
    await expect(page).toHaveURL(/\/roadmap\?target=89$/);
    await expect(page.getByRole('heading', { name: /Action plan to reach 89/ })).toBeVisible();
    await expect(page.getByText(/Calculated for the target you chose/)).toBeVisible();

    const planSteps = page.getByRole('list', { name: 'Steps of the action plan' }).getByRole('listitem').filter({ has: page.locator('ul') });
    await expect(planSteps).toHaveCount(labSteps);

    // Cambiar el objetivo aquí recalcula, y volver al catálogo deja 90.
    await page.getByLabel('Target score').fill('95');
    await page.getByRole('button', { name: 'Recalculate the plan' }).click();
    await expect(page).toHaveURL(/\/roadmap\?target=95$/);
    await expect(page.getByRole('heading', { name: /Action plan to reach 95/ })).toBeVisible();

    await page.getByRole('link', { name: 'Open in What-If' }).click();
    await expect(page).toHaveURL(/target=95/);
    await expect(page.getByText('95', { exact: true })).toBeVisible();

    await page.goBack();
    await page.getByRole('button', { name: 'Back to the catalog target' }).click();
    await expect(page.getByRole('heading', { name: /Action plan to reach 90/ })).toBeVisible();
  });
});
