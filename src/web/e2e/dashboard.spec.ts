import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * Dashboard ejecutivo. Se comprueba que la pregunta ejecutiva y las seis preguntas
 * esten, que cualquier indicador abra el mismo detalle, que el eje de los mapas de
 * calor y los filtros nuevos viajen en la URL, y que el detalle abierto no deje a
 * nadie fuera.
 */
const QUESTIONS = [
  'How healthy is the portfolio?',
  'Which applications should be modernized first?',
  'Where should we reduce complexity?',
  'Where is technology risk concentrated?',
  'What is driving obsolescence?',
  'Where are the resilience gaps?',
];

test.describe('dashboard', () => {
  test('the executive question and the six questions are on one screen', async ({ page }) => {
    await signIn(page, '/');

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Portfolio Health & Risk Intelligence');
    await expect(page.getByText('Where is the highest concentration of portfolio risk')).toBeVisible();

    for (const question of QUESTIONS) {
      await expect(page.getByRole('heading', { name: question })).toBeVisible();
    }

    await expect(page.getByRole('list', { name: 'Portfolio indicators' }).getByRole('listitem')).toHaveCount(5);
  });

  test('an indicator opens the detail, and Escape closes it', async ({ page }) => {
    await signIn(page, '/');

    await page.getByRole('button', { name: /Applications at risk/ }).click();

    const detail = page.getByRole('dialog', { name: 'Applications at risk' });
    await expect(detail).toBeVisible();
    await expect(detail.getByRole('columnheader', { name: 'Resilience' })).toBeVisible();
    await expect(detail.getByRole('link').first()).toBeVisible();

    await page.keyboard.press('Escape');
    await expect(detail).toBeHidden();
  });

  test('the heatmap axis and the new filters travel in the URL', async ({ page }) => {
    await signIn(page, '/');

    await page
      .getByRole('group', { name: 'Heatmap axis' })
      .first()
      .getByRole('button', { name: 'Primary capability' })
      .click();
    await expect(page).toHaveURL(/axis=capability/);

    await page.getByLabel('Primary capability').selectOption({ index: 1 });
    await expect(page).toHaveURL(/capability=/);
    await expect(page).toHaveURL(/axis=capability/);

    // El filtro sigue puesto al cambiar de pantalla, como todos los demas.
    await page.getByRole('link', { name: 'Portfolio', exact: true }).click();
    await expect(page).toHaveURL(/capability=/);
  });

  test('the dashboard is the home page and the first tab of the site', async ({ page }) => {
    await signIn(page);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Portfolio Health & Risk Intelligence');
    const tabs = page.getByRole('banner').getByRole('navigation').getByRole('link');
    await expect(tabs.first()).toHaveText('Dashboard');
  });

  test('the old dashboard address still works and keeps its filters', async ({ page }) => {
    await signIn(page, '/dashboard?axis=capability');

    await expect(page).toHaveURL(/\/\?axis=capability$/);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Portfolio Health & Risk Intelligence');
  });

  test('the health heatmap has one row per CIO and totals that open their applications', async ({ page }) => {
    await signIn(page, '/');

    const health = page.getByRole('region', { name: 'How healthy is the portfolio?' });
    await expect(health.getByRole('columnheader', { name: 'CIO' })).toBeVisible();
    await expect(health.getByRole('columnheader', { name: 'Total' })).toBeVisible();
    await expect(health.getByRole('rowheader', { name: 'Total' })).toBeVisible();
    await expect(health.getByRole('group', { name: 'Heatmap axis' }).getByRole('button', { name: 'CIO' })).toHaveCount(0);

    await health.getByRole('button', { name: /^Whole portfolio: \d+ applications/ }).click();

    const detail = page.getByRole('dialog');
    await expect(detail).toBeVisible();
    await expect(detail).toContainText('Whole portfolio');
  });

  /**
   * Una aplicacion sin roadmap de MAPS entra a la matriz con un plan automatico hasta 90
   * (2026-10-07). Con los datos de ejemplo todas tienen roadmap: la respuesta se ajusta.
   */
  test('an application without a MAPS roadmap shows its automatic plan to 90', async ({ page }) => {
    await page.route('**/api/dashboard*', async (route) => {
      const response = await route.fetch();
      const body = (await response.json()) as {
        dashboard: { prioritization: { top: { roadmapName: string | null; automaticTarget: number | null }[]; automaticPlans: number; automaticTarget: number | null } };
      };
      const prioritization = body.dashboard.prioritization;
      prioritization.top[0] = { ...prioritization.top[0]!, roadmapName: null, automaticTarget: 90 };
      prioritization.automaticPlans = 1;
      prioritization.automaticTarget = 90;
      await route.fulfill({ response, json: body });
    });

    await signIn(page, '/');

    const panel = page.getByRole('region', { name: 'Which applications should be modernized first?' });
    await expect(panel.getByText(/automatic plan to 90/).first()).toBeVisible();
    await expect(panel).toContainText('use an automatic plan with the levers that take them to 90');
  });

  /** Sin ningun plan, la pregunta 2 dice por que en vez de mostrar una matriz vacia. */
  test('without any plan the prioritization says why instead of an empty matrix', async ({ page }) => {
    await page.route('**/api/dashboard*', async (route) => {
      const response = await route.fetch();
      const body = (await response.json()) as {
        dashboard: { applications: unknown[]; prioritization: Record<string, unknown> };
      };
      const total = body.dashboard.applications.length;
      Object.assign(body.dashboard.prioritization, {
        points: [],
        top: [],
        candidates: 0,
        outsideMatrix: total,
        withoutPlan: total,
        automaticPlans: 0,
        automaticTarget: null,
      });
      await route.fulfill({ response, json: body });
    });

    await signIn(page, '/');

    const panel = page.getByRole('region', { name: 'Which applications should be modernized first?' });
    await expect(panel).toContainText('No application can be placed in the prioritization matrix.');
    await expect(panel).toContainText('have no MAPS roadmap in this upload');
    await expect(panel.getByRole('link', { name: 'Data → Mapping' })).toBeVisible();
  });

  test('each panel explains its calculation with the values in force', async ({ page }) => {
    await signIn(page, '/');

    const risk = page.getByRole('region', { name: 'Where is technology risk concentrated?' });
    await risk.getByText('How this is calculated').click();
    await expect(risk.getByText(/moderate from 40, high from 60, critical from 80/)).toBeVisible();
    await expect(risk.getByText('Provisional', { exact: true })).toBeVisible();

    // Las fórmulas, de qué reporte y columna sale cada dato, y el cálculo con lo que se ve.
    await expect(risk.getByText('Risk Score = (100 − Pillar) ÷ 80 × 100', { exact: false })).toBeVisible();
    await expect(risk.getByRole('cell', { name: /Kenna_Score/ })).toBeVisible();
    await expect(risk.getByText('With the current data')).toBeVisible();
    await expect(risk.getByText(/of the risk mass, index/)).toBeVisible();

    await page.getByText('How the indicators are calculated').click();
    await expect(page.getByText(/Currency Score below 60/).first()).toBeVisible();

    await risk.getByRole('link', { name: 'See every definition' }).click();
    await expect(page).toHaveURL(/\/definitions#risk$/);
  });

  for (const colorScheme of ['light', 'dark'] as const) {
    test(`the open detail has no serious accessibility violations in ${colorScheme} mode`, async ({ page }) => {
      await page.emulateMedia({ colorScheme });
      await signIn(page, '/');
      await page.getByRole('button', { name: /Modernization candidates/ }).click();
      await expect(page.getByRole('dialog')).toBeVisible();

      const results = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
        .analyze();

      const serious = results.violations
        .filter((violation) => violation.impact === 'serious' || violation.impact === 'critical')
        .map((violation) => `${violation.id}: ${violation.nodes.length} · ${violation.help}`);

      expect(serious).toEqual([]);
    });
  }
});
