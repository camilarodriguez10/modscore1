"""Fase 14 - TD-04: contraste del texto atenuado y aria-label en barras no interactivas."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")
web = root / "src/web"


def patch(rel, pairs, count=1):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if new in text:
            raise SystemExit("ya aplicado: " + rel)
        if text.count(old) < count:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:140])
        text = text.replace(old, new, count)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


# ----------------------------------------------------------------- contraste
# #6d7a81 daba 4.42:1 sobre blanco y 3.89:1 sobre el fondo de pagina: bajo el minimo
# de 4.5:1 de WCAG AA. #5f6c73 da 5.41, 5.12 y 4.77.
patch("src/web/src/index.css", [
    (
        "  --app-ink-3: #6d7a81;",
        "  /* Texto atenuado: 4.5:1 como minimo sobre page, surface y surface-2 (WCAG AA). */\n  --app-ink-3: #5f6c73;",
    ),
])

# En oscuro, #7b8a91 daba 4.48:1 sobre surface-2. #828f96 da 4.81.
patch("src/web/src/index.css", [("    --app-ink-3: #7b8a91;", "    --app-ink-3: #828f96;")])
patch("src/web/src/index.css", [("  --app-ink-3: #7b8a91;", "  --app-ink-3: #828f96;")])

# ------------------------------------------------- aria-label en divs sin rol
# ARIA no permite aria-label en un div sin rol. Estas barras son una imagen con
# texto alternativo: role="img". Los tramos interactivos ya son <button>.
patch("src/web/src/components/charts.tsx", [
    (
        """            <div
              className="min-w-[3px] rounded-r-sm bg-grid"
              style={{ flexGrow: unearned, flexBasis: 0 }}
              aria-label={`Not earned: ${fmt(unearned)} points`}
            />""",
        """            <div
              role="img"
              className="min-w-[3px] rounded-r-sm bg-grid"
              style={{ flexGrow: unearned, flexBasis: 0 }}
              aria-label={`Not earned: ${fmt(unearned)} points`}
            />""",
    ),
    (
        """        <div
          className="rounded-l-sm bg-accent"
          style={{ flexGrow: available, flexBasis: 0 }}
          aria-label={`Weight with data: ${fmt(available, 0)}`}
        />""",
        """        <div
          role="img"
          className="rounded-l-sm bg-accent"
          style={{ flexGrow: available, flexBasis: 0 }}
          aria-label={`Weight with data: ${fmt(available, 0)}`}
        />""",
    ),
    (
        """          <div
            className="rounded-r-sm bg-grid"
            style={{ flexGrow: total - available, flexBasis: 0 }}
            aria-label={`Weight without data: ${fmt(total - available, 0)}`}
          />""",
        """          <div
            role="img"
            className="rounded-r-sm bg-grid"
            style={{ flexGrow: total - available, flexBasis: 0 }}
            aria-label={`Weight without data: ${fmt(total - available, 0)}`}
          />""",
    ),
])

# ------------------------------------------------- prueba en claro y oscuro
spec = '''import { AxeBuilder } from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { signIn } from './support';

/**
 * Accesibilidad (TD-04). Revisa con axe las pantallas que alguien usa a diario y una de
 * administracion, en claro y en oscuro. Falla con las violaciones serias y criticas: son
 * las que dejan a alguien fuera, no un detalle de estilo.
 */
const screens = [
  { name: 'portfolio', path: '/' },
  { name: 'calculation of the BRD example', path: '/applications/14381/calculation' },
  { name: 'roadmap', path: '/roadmap' },
  { name: 'data', path: '/data' },
  { name: 'rules administration', path: '/admin/rules' },
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
'''

(web / "e2e/accessibility.spec.ts").write_text(spec.replace("\n", "\r\n"), encoding="utf-8", newline="")
print("escrito e2e/accessibility.spec.ts")

dump = web / "e2e/axe-dump.spec.ts"
if dump.exists():
    dump.unlink()
    print("borrado e2e/axe-dump.spec.ts")
