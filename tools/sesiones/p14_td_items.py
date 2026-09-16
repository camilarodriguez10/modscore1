"""Fase 14 - estado de TD-04 y TD-05 en los pendientes."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if old not in text:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:160])
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


pending = "docs/PENDING-ITEMS.md"

patch(pending, [
    (
        "| TD-04 | Visual QA and accessibility audit of the new screens | Quality | P1 | UX + development | Open |",
        "| TD-04 | Visual QA and accessibility audit of the new screens | Quality | P1 | UX + development | Automated part done (2026-09-15); manual walkthrough open |",
    ),
    (
        "| TD-05 | End-to-end tests in CI | Quality | P1 | DevSecOps | Open |",
        "| TD-05 | End-to-end tests in CI | Quality | P1 | DevSecOps | Done (2026-09-15) |",
    ),
    (
        """- **Priority:** P1 · **Owner:** UX + development · **Status:** Open
- **Context:**
  - Screens are **Rules** (phase 12), **Compare rules**, **trend markers**, the **rules notice** and **Download CSV** buttons (phase 13).
  - Functional behavior is covered by end-to-end tests.
  - No visual review on several widths and no accessibility audit (axe, screen reader, keyboard only) has been done for them.
- **Work:** axe scan (can be added to Playwright with `@axe-core/playwright` after a vulnerability check); keyboard-only walkthrough; review at 1280, 1024 and 768 px; contrast of the highlighted rows.
- **Acceptance criteria:** zero serious or critical axe violations; walkthrough notes resolved. **Effort:** M.""",
        """- **Priority:** P1 · **Owner:** UX + development · **Status:** Automated part done (2026-09-15); the manual walkthrough is still open
- **Context:**
  - Screens are **Rules** (phase 12), **Compare rules**, **trend markers**, the **rules notice** and **Download CSV** buttons (phase 13).
  - Functional behavior is covered by end-to-end tests.
- **Done (2026-09-15):** `@axe-core/playwright` 4.13.0 (no vulnerabilities) scans five screens in light and dark inside the e2e suite and in CI, failing on serious or critical violations. It found two real problems, both fixed:
  - muted text at 4.42:1 on white and 3.89:1 on the page background, below the 4.5:1 minimum. `--app-ink-3` is now `#5f6c73` in light and `#8b989e` in dark;
  - status colours used as text (down to 1.65:1). They stay as marks and text now uses `--app-*-text` variants that meet AA on both the surface and their own wash;
  - three decorative bars carried `aria-label` on a `div` with no role: they declare `role="img"`.
- **Work left:** keyboard-only walkthrough, screen reader pass and review at 1280, 1024 and 768 px. A person has to do those.
- **Acceptance criteria:** zero serious or critical axe violations (met, and guarded by CI); walkthrough notes resolved. **Effort:** S left.""",
    ),
    (
        """- **Priority:** P1 · **Owner:** DevSecOps · **Status:** Open
- **Context:**
  - Six Playwright scenarios run locally with the installed Microsoft Edge (`channel: 'msedge'`), so no browser is downloaded.
  - They start System, Channel and Vite, or reuse running ones; they can also target a running portable package with `MODSCORE_E2E_BASE_URL`.
  - Run time is about 30 s after builds.
- **Work:** CI job with .NET 10, Node 24 and Edge or Chromium (a runner image with a browser, or `npx playwright install` with its supply-chain review); upload traces on failure.
- **Acceptance criteria:** e2e job required on pull requests. **Effort:** S–M.""",
        """- **Priority:** P1 · **Owner:** DevSecOps · **Status:** Done (2026-09-15)
- **Context:**
  - Sixteen Playwright scenarios (6 flows and 10 accessibility checks) run locally with the installed Microsoft Edge (`channel: 'msedge'`), so no browser is downloaded.
  - They start System, Channel and Vite, or reuse running ones; they can also target a running portable package with `MODSCORE_E2E_BASE_URL`.
  - Run time is about 45 s after builds.
- **Done:** the `SPA de punta a punta` job in `ci.yml` installs .NET 10, Node 24 and Playwright's Chromium (`MODSCORE_E2E_CHANNEL=chromium`), builds both layers, runs the suite and uploads traces when it fails.
- **Work left:** make the job a required status check when branch protection is configured (OP-04).
- **Acceptance criteria:** e2e job required on pull requests. **Effort:** S left.""",
    ),
])
