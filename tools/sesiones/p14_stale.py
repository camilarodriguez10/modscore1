"""Fase 14 - repasa lo que quedo desactualizado en README, PLAN, HANDOFF y pendientes."""
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


# ----------------------------------------------------------------- README
patch("README.md", [
    (
        "640 pruebas .NET: 200 del motor, 175 de contratos y 265 de API. Las dos que importan más",
        "646 pruebas .NET: 200 del motor, 178 de contratos y 268 de API. Las dos que importan más",
    ),
    (
        "npm --prefix src/web run test:e2e  # 6 escenarios de punta a punta (Playwright + Edge)",
        "npm --prefix src/web run test:e2e  # 16 escenarios: 6 de flujo y 10 de accesibilidad (axe)",
    ),
])

# ------------------------------------------------------------------- PLAN
patch("PLAN.md", [
    (
        "| Tests | Vitest + Testing Library + Playwright | **Vitest** (lógica pura, sin DOM) + **Playwright con el Edge instalado** (6 escenarios de punta a punta) |",
        "| Tests | Vitest + Testing Library + Playwright | **Vitest** (lógica pura, sin DOM) + **Playwright** (16 escenarios: 6 de flujo y 10 de accesibilidad con axe; Edge en local, Chromium en el CI) |",
    ),
    (
        "| ASVS L2 al cierre de la Fase 08: 100 cumple · 15 parciales · 3 pendientes. **Vigente desde la Fase 12: 103 cumple · 14 parciales · 3 pendientes · 10 IdP o N/A** | [docs/security/asvs-l2.md](docs/security/asvs-l2.md) |",
        "| ASVS L2 al cierre de la Fase 08: 100 cumple · 15 parciales · 3 pendientes. **Vigente desde la Fase 14: 106 cumple · 11 parciales · 3 pendientes · 10 IdP o N/A** | [docs/security/asvs-l2.md](docs/security/asvs-l2.md) |",
    ),
    (
        "│  ├─ adr/                           # decisiones de arquitectura (0001–0020)",
        "│  ├─ adr/                           # decisiones de arquitectura (0001–0021)",
    ),
    (
        "│  └─ runbooks/                      # secretos, carga defectuosa, DR, incidente, reglas, respaldo en git",
        "│  └─ runbooks/                      # secretos, carga defectuosa, DR, incidente, reglas, respaldo en git, verificación ASVS",
    ),
])

# --------------------------------------------------------------- HANDOFF
patch("HANDOFF.md", [
    (
        "| Artefacto del plan | <https://claude.ai/artifact/5cFSpe2puMikBkB3u5HRKd> (versión 16, con la Fase 13; copia en `docs/plan/modscore-plan.html`;",
        "| Artefacto del plan | <https://claude.ai/artifact/5cFSpe2puMikBkB3u5HRKd> (versión 17, del 2026-09-16, con el avance de la Fase 14; copia en `docs/plan/modscore-plan.html`, que hay que reemplazar al republicar;",
    ),
    ("- estado tras la Fase 13.", "- estado tras la Fase 14."),
])

# --------------------------------------------------------- PENDING-ITEMS
patch("docs/PENDING-ITEMS.md", [
    (
        """  - 640 .NET tests: 200 engine, 175 contracts, 265 API.
  - 35 SPA unit tests.
  - 6 end-to-end scenarios in Microsoft Edge.
  - Zero vulnerable packages, and `npm audit` is clean.""",
        """  - 646 .NET tests: 200 engine, 178 contracts, 268 API.
  - 35 SPA unit tests.
  - 16 end-to-end scenarios: 6 flows and 10 accessibility checks with axe, in light and dark. Microsoft Edge locally, Chromium in CI.
  - Zero vulnerable packages, and `npm audit` is clean.""",
    ),
])
