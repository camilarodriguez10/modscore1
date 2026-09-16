"""Fase 14 - documentacion: PENDING-ITEMS, README, modelo de amenazas."""
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


# --------------------------------------------------------- PENDING-ITEMS.md
pending = "docs/PENDING-ITEMS.md"

patch(pending, [
    # Resumen por categoria
    (
        "| SEC-01 … SEC-14 | ASVS L2 partial requirements | Security | P1 | Security + platform | Open (phase 14) |",
        "| SEC-01 … SEC-14 | ASVS L2 partial requirements | Security | P1 | Security + platform | SEC-06, SEC-07, SEC-12 done; SEC-14 accepted with compensating controls; the rest need a test environment |",
    ),
    # SEC-06
    (
        "| SEC-06 | 7.1.3 | Security events logged | Sessions, denials, CSRF, rate limits, uploads, scope, rules publications and rejections, step-up, **exports (4213, new)** | Rejected validations (400) are not audited | Decide whether 400 on sensitive endpoints must be audited; add event | S |",
        "| SEC-06 | 7.1.3 | Security events logged | **Done (2026-09-15).** Every `/api` response that rejects the input (400, 413, 415, 422) logs event **4214** with the path and the status code, from a middleware in the Channel layer, so no endpoint can forget. The values that were sent are never logged | — | `ValidationAuditTests` | S |",
    ),
    # SEC-07
    (
        "| SEC-07 | 8.3.4 | Retention policy for sensitive data | `Database:RetainVersions` for the database adapter; manual for files | Business retention policy (OP-03) | Documented policy and configured retention | S |",
        "| SEC-07 | 8.3.4 | Retention policy for sensitive data | **Done (2026-09-15).** `Uploads:RetainVersions` keeps the N most recent uploads in the file adapter too; 0 keeps all. It only deletes directories with a manifest, so the rules store and the staging area are safe | How many to keep is a business decision (OP-03) | `FileUploadRetentionTests` | S |",
    ),
    # SEC-12
    (
        "| SEC-12 | 13.2.2 | JSON schema validation | Typed DTOs, `BoundedJson` (128 KB, strict JSON, no unknown fields) for rules, explicit checks | No formal schema validation | Decide if strict typed binding is enough or add schema validation for rules bodies | S–M |",
        "| SEC-12 | 13.2.2 | JSON schema validation | **Decided (2026-09-15).** Strict typed binding stays: `BoundedJson` (128 KB, no unknown members, nullable annotations and required parameters respected, depth 16) plus the `Ruleset.Validate` invariants. Adding a schema library would mean one more dependency and two sources of truth | — | [ADR 0021](adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md) | S |",
    ),
    # SEC-14
    (
        "| SEC-14 | 14.2.6 | Isolation of third-party components | Containers, NetworkPolicy | No per-library sandbox | Accept as design limit or document compensating controls | S |",
        "| SEC-14 | 14.2.6 | Isolation of third-party components | **Accepted (2026-09-15)** as a design limit: neither .NET nor Node sandboxes a single library. Compensating controls: minimal locked dependency tree, `npm ci --ignore-scripts`, NuGetAudit, `npm audit`, OSV and Trivy on every push, chiseled images with no shell, non-root, read-only filesystem and dropped capabilities, NetworkPolicy, and a strict CSP with no third-party resources | — | `asvs-l2.md` 14.2.6 | S |",
    ),
])

# Inventario de pruebas
patch(pending, [
    (
        "| SPA end-to-end (Playwright, Edge) | 6 | `npm --prefix src/web run test:e2e` |",
        "| SPA end-to-end (Playwright, Edge locally, Chromium in CI) | 6 | `npm --prefix src/web run test:e2e` |\n| SPA accessibility (axe, five screens in light and dark) | 10 | Same command; `e2e/accessibility.spec.ts` |",
    ),
])

# ------------------------------------------------------------------ README
patch("README.md", [
    (
        "| `Portfolio__WarmupSeconds` | System | 15 | Cada cuánto se dejan calculados scores e historia; 0 lo apaga |",
        "| `Portfolio__WarmupSeconds` | System | 15 | Cada cuánto se dejan calculados scores e historia; 0 lo apaga |\n| `Uploads__RetainVersions` | System | 0 | Cuántas cargas conserva el adaptador de archivos; 0 conserva todas |",
    ),
])

# ------------------------------------------------- modelo de amenazas: 4214
patch("docs/security/modelo-de-amenazas.md", [
    (
        "| **T** | Acción de CI secuestrada por etiqueta | Todas las acciones fijadas por SHA | `ci.yml` · `seguridad.yml` | — |",
        "| **T** | Acción de CI secuestrada por etiqueta | Todas las acciones fijadas por SHA | `ci.yml` · `seguridad.yml` | — |\n| **R** | Sondeo del borde sin rastro | Toda entrada rechazada por validación deja el evento 4214 con ruta y código | Middleware de la capa Channel · `ValidationAuditTests` | Alerta del SIEM sobre ráfagas de 4214 (SEC-11) |",
    ),
])
