import io
import os

ROOT = r"C:\Users\kmiro\Downloads\mod score"
FENCE = "`" * 3


def load(name):
    raw = io.open(os.path.join(ROOT, name), encoding="utf-8", newline="").read()
    return raw.replace("\r\n", "\n"), "\r\n" in raw


def save(name, s, crlf):
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(os.path.join(ROOT, name), "w", encoding="utf-8", newline="").write(s)
    print("ok", name)


# ------------------------------------------------------------------ PLAN.md
s, crlf = load("PLAN.md")

replacements = [
    ("│  · Rate limiting, CSRF, cabeceras de seguridad, CSP nonce            │",
     "│  · Rate limiting, CSRF, cabeceras de seguridad, CSP estricta         │"),
    ("- **CSP estricta** con nonce por request: `default-src 'self'; script-src 'self' 'nonce-…'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'`. Sin `unsafe-inline`, sin `unsafe-eval`.",
     "- **CSP estricta, sin nonce**: `default-src 'self'; script-src 'self'; style-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'`. Sin `unsafe-inline`, sin `unsafe-eval` y sin orígenes de terceros. El build de producción no tiene un solo script ni estilo en línea, así que `'self'` es más estricto que un nonce — verificado en la Fase 08 con cero violaciones en navegador. Las fuentes se sirven desde el mismo origen."),
    ("- Subresource Integrity y `Permissions-Policy` restrictiva.",
     "- `Permissions-Policy` restrictiva. Subresource Integrity no aplica: todos los recursos son del mismo origen."),
    ("| **8 — Endurecimiento** | 2 sem | Pentest, ASVS L2, carga, DR, runbooks | Sin hallazgos High/Critical abiertos |",
     "| **8 — Endurecimiento** ✅ | ENTREGADA | Pentest, ASVS L2, carga, DR, runbooks | **Cumplido:** 23 hallazgos, las 3 Altas cerradas, ninguna High/Critical abierta |"),
]

for old, new in replacements:
    assert old in s, f"PLAN: {old[:70]!r}"
    s = s.replace(old, new, 1)

rfc08_start = s.index("| RFC-08 | Huecos entre bandas del BRD |")
rfc08_end = s.index("\n", rfc08_start)
s = s[:rfc08_end] + """
| RFC-10 | Claim de alcance del IdP | La autorización por LOB/CIO lee `portfolio_scope` con valores `*`, `lob:…`, `country:…`, `cio:…`; el nombre es configurable. Sin claim no se ve nada — ver [ADR 0012](docs/adr/0012-el-alcance-se-aplica-sobre-los-datos.md) |
| RFC-11 | Cache compartido de sesiones | Sesiones en el servidor con cache en memoria: una réplica de channel-api hasta registrar Redis — ver [ADR 0011](docs/adr/0011-la-sesion-vive-en-el-servidor.md) |
| RFC-12 | Identidad de carga de trabajo | Token de servicio estático con rotación sin corte y mTLS; tokens de vida corta según la plataforma del cluster — ver H-16 en [hallazgos](docs/security/hallazgos.md) |""" + s[rfc08_end:]

anchor = "Total: **~16 semanas**. Las fases 7 y 9 son deliberadamente cortas — el trabajo de aislamiento se hace en la fase 2, no cuando llegan los archivos reales.\n"
assert anchor in s, "PLAN: total"
s = s.replace(anchor, anchor + """
### 8.1 Resultado de la Fase 08 — Endurecimiento

**Criterio cumplido: ninguna High ni Critical abierta.** La revisión encontró 23
hallazgos; las tres Altas se cerraron con pruebas que las reproducen. Detalle en
[docs/security/hallazgos.md](docs/security/hallazgos.md).

| Severidad | Encontrados | Cerrados | Abiertos |
|---|---:|---:|---|
| Alta | 3 | 3 | — |
| Media | 10 | 8 | H-16 parcial (token de servicio estático) · H-17 (firma de imágenes y logs inmutables: infraestructura) |
| Baja | 8 | 6 | H-20 (ver y cerrar sesiones activas) · H-21 (antivirus en cargas) |
| Informativa | 2 | 2 | — |

Las tres Altas:

1. **Cualquier sesión podía publicar una carga.** Ahora exige el rol `ingestion.write` y alcance completo, en las dos capas.
2. **La autorización por LOB/CIO de la sección 7.2 no existía.** Todos veían todo, incluidas las vulnerabilidades por aplicación. Ahora el alcance se aplica en la capa System, y ninguna lectura del portafolio compila sin él.
3. **El rate limiter corría antes de autenticar.** Todos compartían el balde de la IP del ingress: uno solo agotaba el servicio.

| Entregable | Dónde |
|---|---|
| Modelo de amenazas STRIDE por frontera | [docs/security/modelo-de-amenazas.md](docs/security/modelo-de-amenazas.md) |
| ASVS L2: 100 cumple · 15 parciales · 3 pendientes | [docs/security/asvs-l2.md](docs/security/asvs-l2.md) |
| Runbooks: secretos, carga defectuosa, DR, incidente | [docs/runbooks/](docs/runbooks) |
| Pipeline con gates de seguridad | `.github/workflows/ci.yml` — acciones fijadas por SHA, CodeQL, Gitleaks, OSV, Trivy, Checkov, SBOM |
| Contenedores y cluster | `deploy/docker` (chiseled, no root) · `deploy/k8s` (PSA restricted, NetworkPolicy default-deny, mTLS) |
| Prueba de carga | `tools/Modernization.LoadTest` |
| Decisiones | ADR 0011 (sesión en el servidor) · 0012 (alcance sobre los datos) · 0013 (autenticado no significa inofensivo) |

**Medido antes y después**, con 30 versiones en la historia:

| | Antes | Después |
|---|---:|---:|
| Historia del portafolio | 1,0 s | 0,006 s |
| Historia de una aplicación | 0,8 s | 0,005 s |
| Pantalla Cargas, 16 usuarios concurrentes | — | 4 241 req/s · p95 5,5 ms |
| JSON de 2 MB contra cualquier endpoint | aceptado | 413 |
| Carga de 40 MB | cortada a 30 MB | aceptada en 3,3 s |

**Desviación del plan, a propósito:** la CSP no usa nonce (sección 6.3).

**No se ejecutó en local:** DAST con OWASP ZAP, Trivy y la construcción de las
imágenes, porque requieren descargar imágenes de contenedor. Quedan en CI.
""", 1)

structure_head = "## 9. Estructura del repositorio\n\n" + FENCE + "\n"
start = s.index(structure_head) + len(structure_head)
end = s.index("\n" + FENCE + "\n\n---\n\n## 10.", start)
s = s[:start] + """modernization-score/
├─ Directory.Packages.props          # versiones centralizadas
├─ Directory.Build.props             # warnings como errores, todas las reglas de seguridad, lock files
├─ .github/workflows/ci.yml          # tests, vulnerabilidades, CodeQL, Gitleaks, OSV, Trivy, Checkov, SBOM
├─ renovate.json · .gitleaks.toml
├─ docs/
│  ├─ adr/                           # decisiones de arquitectura (0001–0013)
│  ├─ security/                      # modelo de amenazas, ASVS L2, hallazgos
│  └─ runbooks/                      # secretos, carga defectuosa, DR, incidente
├─ contracts/
│  ├─ ruleset-v1.3.json              # pesos, bandas, estrategias
│  ├─ roadmap-v1.json                # modelo MAPS
│  └─ sources/source-schemas.json    # contratos de columnas por fuente
├─ data/dummy/                       # CSV generados
├─ src/
│  ├─ shared/
│  │  ├─ Modernization.Domain/          # modelo canónico + puertos
│  │  ├─ Modernization.Scoring.Engine/  # motor puro — el activo crítico
│  │  ├─ Modernization.WhatIf/ · Modernization.Roadmap/
│  │  └─ Modernization.Security/        # alcance, auditoría, cabeceras, token de servicio
│  ├─ channel/
│  │  └─ Modernization.ChannelApi/      # BFF / orquestador; sirve la SPA en despliegue
│  ├─ system/
│  │  ├─ Modernization.SystemApi/       # host HTTP de la capa System
│  │  └─ Modernization.Sources/         # adaptadores de fuentes
│  │     └─ {Dummy,Files,Database}/
│  └─ web/                              # React 19 SPA
├─ tests/
│  ├─ Modernization.Scoring.Engine.Tests/     # golden tests, tabla de casos, what-if, roadmap
│  ├─ Modernization.Sources.ContractTests/    # una suite, dos adaptadores (tres en la Fase 09)
│  └─ Modernization.Api.Tests/                # E2E por ambas capas y seguridad
├─ tools/
│  ├─ Modernization.DummyData/       # generador determinista
│  └─ Modernization.LoadTest/        # prueba de carga sin dependencias
└─ deploy/
   ├─ docker/                        # imágenes chiseled, no root
   └─ k8s/                           # kustomize, PSA restricted, NetworkPolicy, mesh""" + s[end:]

last = "7. **Destino de despliegue** — AKS (coherente con las menciones a AKS/ASE del BRD) o alternativa."
assert last in s, "PLAN: seccion 10"
s = s.replace(last, last + """
8. **RFC-10** — Claim de alcance del IdP: nombre (`portfolio_scope` por defecto) y valores (`*`, `lob:…`, `country:…`, `cio:…`). ¿Lo emite el IdP corporativo, o se deriva de grupos?
9. **RFC-11** — Cache compartido para las sesiones (Redis) antes de escalar channel-api a más de una réplica.
10. **RFC-12** — Identidad de carga de trabajo de vida corta entre channel-api y system-api, según la plataforma del cluster.""", 1)

save("PLAN.md", s, crlf)

# ---------------------------------------------------------------- README.md
s, crlf = load("README.md")

security_block = """Ambos deben reportar cero. El gate no es decorativo: ya rompió el build una vez,
cuando `Microsoft.AspNetCore.OpenApi` arrastró un `Microsoft.OpenApi` con una
vulnerabilidad alta conocida.

El build compila con **todas** las reglas de seguridad del analizador .NET como
errores, y el restore usa lock files (`packages.lock.json`).

## Seguridad

El resultado del endurecimiento (Fase 08) está en [docs/security](docs/security):
modelo de amenazas, matriz ASVS L2 y registro de hallazgos. Los procedimientos de
operación —rotación de secretos, carga defectuosa, recuperación ante desastres,
incidente— están en [docs/runbooks](docs/runbooks).

El usuario de desarrollo tiene todos los permisos. Para reproducir a uno limitado,
fija sus roles y su alcance al levantar el channel-api:

""" + FENCE + """bash
Authentication__Development__Roles__0=portfolio.read Authentication__Development__Scopes__0=lob:Individual dotnet run --project src/channel/Modernization.ChannelApi
""" + FENCE + """

## Prueba de carga

""" + FENCE + """bash
dotnet run --project tools/Modernization.LoadTest -- --duration 15 --concurrency 16
""" + FENCE + """

Mide la capa System directamente y la Channel con una sesión de desarrollo. La
Channel limita a 120 peticiones por minuto por usuario: para medirla hay que
levantarla con `RateLimiting__RequestsPerMinute` alto.
"""

replacements = [
    ("351 tests.", "435 tests."),
    ("""Ambos deben reportar cero. El gate no es decorativo: ya rompió el build una vez,
cuando `Microsoft.AspNetCore.OpenApi` arrastró un `Microsoft.OpenApi` con una
vulnerabilidad alta conocida.
""", security_block),
    ("tests/                          golden tests, contract tests, E2E y seguridad\ntools/                          generador de datos dummy",
     "tests/                          golden tests, contract tests, E2E y seguridad\ntools/                          generador de datos dummy y prueba de carga\ndocs/                           ADR, seguridad y runbooks\ndeploy/                         imágenes y manifiestos del cluster"),
    ("| 08 Endurecimiento | pendiente |", "| 08 Endurecimiento | entregada |"),
    ("RFC-01 a\nRFC-08.", "RFC-01 a\nRFC-12."),
]

for old, new in replacements:
    assert old in s, f"README: {old[:70]!r}"
    s = s.replace(old, new, 1)

save("README.md", s, crlf)
