import { expect, type Download, type Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';

/** Inicia sesion con el esquema de desarrollo si hace falta y deja el portafolio a la vista. */
export async function signIn(page: Page, path = '/') {
  await page.goto(path);

  const signInButton = page.getByRole('button', { name: 'Sign in' });
  const portfolioTab = page.getByRole('link', { name: 'Portfolio', exact: true });

  await expect(signInButton.or(portfolioTab)).toBeVisible();

  if (await signInButton.isVisible()) {
    await signInButton.click();
    await expect(portfolioTab).toBeVisible();
  }
}

/** Hace clic en un enlace de descarga y devuelve el archivo con sus filas. */
export async function download(page: Page, click: () => Promise<void>): Promise<{ file: Download; rows: string[] }> {
  const [file] = await Promise.all([page.waitForEvent('download'), click()]);
  const path = await file.path();
  // Los CSV empiezan con BOM para que Excel los lea como UTF-8.
  const text = (await readFile(path, 'utf8')).replace(/^\uFEFF/, '');

  return { file, rows: text.split('\r\n').filter((line) => line.length > 0) };
}
