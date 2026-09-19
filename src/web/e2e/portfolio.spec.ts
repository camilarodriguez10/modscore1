import { expect, test } from '@playwright/test';
import { download, signIn } from './support';

test.describe('portfolio', () => {
  test('a filter chosen from a chart travels with the URL across screens', async ({ page }) => {
    await signIn(page, '/portfolio');

    await page.getByRole('button', { name: /^Mexico: / }).first().click();
    await expect(page).toHaveURL(/country=Mexico/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('application');

    await page.getByRole('link', { name: 'Roadmap', exact: true }).click();
    await expect(page).toHaveURL(/\/roadmap\?.*country=Mexico/);
    await expect(page.getByRole('heading', { level: 1 })).toContainText('with a plan');
  });

  test('the filtered portfolio downloads as CSV', async ({ page }) => {
    await signIn(page, '/portfolio?country=Mexico');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('application');

    const { file, rows } = await download(page, () => page.getByRole('link', { name: 'Download CSV' }).click());

    expect(file.suggestedFilename()).toMatch(/^modscore-portfolio-\d{8}-\d{4}\.csv$/);
    expect(rows[0]).toContain('EAI Code,Application');
    expect(rows.length).toBeGreaterThan(1);

    for (const row of rows.slice(1)) {
      expect(row).toContain(',Mexico,');
    }
  });

  test('the BRD worked example explains its calculation: 79.67', async ({ page }) => {
    await signIn(page, '/applications/14381/calculation');

    await expect(page.getByText('79.67').first()).toBeVisible();
  });

  test('the roadmap downloads as CSV with its grouping', async ({ page }) => {
    await signIn(page, '/roadmap?groupBy=country');
    await expect(page.getByRole('heading', { level: 1 })).toContainText('with a plan');

    const { file, rows } = await download(page, () => page.getByRole('link', { name: 'Download CSV' }).click());

    expect(file.suggestedFilename()).toMatch(/^modscore-roadmap-.+\.csv$/);
    expect(rows[0]).toContain('Grouping,Group,Applications');
    expect(rows.length).toBeGreaterThan(1);
  });
});
