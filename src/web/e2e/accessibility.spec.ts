import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * Accesibilidad (TD-04). Revisa con axe las pantallas que alguien usa a diario y una de
 * administracion, en claro y en oscuro. Falla con las violaciones serias y criticas: son
 * las que dejan a alguien fuera, no un detalle de estilo.
 */
const screens = [
  { name: 'dashboard', path: '/' },
  { name: 'portfolio', path: '/portfolio' },
  { name: 'definitions', path: '/definitions' },
  { name: 'calculation of the BRD example', path: '/applications/14381/calculation' },
  { name: 'insights', path: '/insights' },
  { name: 'roadmap', path: '/roadmap' },
  { name: 'data', path: '/data' },
  { name: 'rules administration', path: '/admin/rules' },
  { name: 'portfolio analytics administration', path: '/admin/rules?tab=analytics' },
];

for (const scheme of ['light', 'dark'] as const) {
  test.describe(`accessibility in ${scheme} mode`, () => {
    test.use({ colorScheme: scheme });

    for (const screen of screens) {
      test(`${screen.name} has no serious accessibility violations`, async ({ page }) => {
        await signIn(page, screen.path);
        await page.waitForLoadState('networkidle');

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
}
