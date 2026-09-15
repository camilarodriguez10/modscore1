"""Fase 12: PLAN.md y README.md."""
import io

ROOT = "C:/Users/kmiro/Downloads/mod score/"


def patch(rel, pairs):
    path = ROOT + rel
    raw = io.open(path, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        count = s.count(old)
        assert count == 1, (rel, count, old[:80])
        s = s.replace(old, new)
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print("ok", rel)


SECTION_85 = """### 8.5 Resultado de la Fase 12 — Administración de reglas

**Criterio cumplido, verificado en vivo y por una prueba de punta a punta.**

En vivo, sobre las 250 aplicaciones de la historia local:

- **Cambio.** Desde la pantalla Rules se pasaron 3 puntos de peso de Kenna a MFA.
- **Simulación.** Mostró el efecto antes de publicar: el promedio pasaba de 70,52 a 70,68,
  con 101 aplicaciones que suben y 65 que bajan.
- **Publicación.** v1.4 quedó vigente en el mismo proceso y el portafolio pasó a 70,68,
  sin recompilar ni reiniciar.
- **Auditoría.** Registró
  `Reglas publicadas. usuario=dev-user version=v1.4 base=v1.3 cambios=2 suben=101 bajan=65 deltaPromedio=0.16`.
- **Restauración.** Cargar v1.3 desde la pestaña de versiones y publicarla creó v1.5 con
  el contenido de fábrica, y el portafolio volvió a 70,52.

| Evidencia | Resultado |
|---|---|
| Pruebas | **598 en verde** (eran 524): 190 motor · 235 API · 173 contratos. Cero paquetes vulnerables en 15 proyectos y `npm audit` limpio |
| Criterio de aceptación | `PublishingAWeightMovesThePortfolioIsAuditedAndNeedsNoRedeploy` publica desde la capa Channel. Comprueba que el promedio del portafolio cambia, que la 14381 puntúa con v1.4 y con el mismo motor que simuló, y que queda el evento 4211 |
| Sin redesplegar, con réplicas | `AnotherReplicaTakesThePublicationWithoutRestarting`: dos hosts sobre el mismo almacén, y lo que publica uno lo lee el otro |
| Cálculo a mano | EAI 10969 con Kenna en 5 y MFA en 10: numerador 62,009 sobre 80, score **82,01** |
| Almacenes | La misma suite de contrato corre sobre memoria, archivos y SQLite |
| Seguridad | Rol propio, reautenticación, permisos que la System vuelve a exigir, CSRF, límites y JSON estricto y acotado, con 17 pruebas de API. ASVS 3.7.1 y 4.3.3 pasan a cumple |

**Qué se construyó:**

- **Reglas como versiones publicadas** ([ADR 0018](docs/adr/0018-las-reglas-se-publican-como-versiones.md)).
  Ruleset, palancas y roadmap se publican juntos. Las de fábrica son v1.3, cada
  publicación crea una versión nueva y volver atrás es publicar la anterior.
- **Se editan valores, no estructura:**
  - pesos: el total tiene que dar 100 y el de cada pilar es la suma de sus métricas;
  - bandas, y puntajes de casos y de categorías;
  - qué palancas se ofrecen y su cantidad por defecto;
  - los umbrales del roadmap, las palancas de cada roadmap y las semanas de cada hito.
- **Borrador, simulación y publicación.** El impacto lo calcula el servidor sobre todo el
  portafolio: cuántas suben y cuántas bajan, el promedio, los tramos, las diez que más se
  mueven, la proyección de los planes y las alertas.
- **Almacén con el mismo patrón que las fuentes:** memoria, archivos en `.rules` y la
  tabla `rules_version` con su migración.
- **Pantalla Rules en la SPA**, solo para `ruleset.admin` con alcance completo. La
  reautenticación está integrada: el borrador sobrevive a la ida y vuelta al IdP.
- **Los objetivos de las palancas salen de las bandas.** Eran constantes que podían
  quedar desalineadas con el ruleset.

**Dos decisiones que corrigen o precisan el plan:**

1. **La historia se puntúa con las reglas vigentes.** La sección 8bis.3 decía que cada
   carga conservaría su versión de reglas, pero eso mezclaría en una misma tendencia un
   cambio de datos con uno de reglas. Lo que no se reescribe es el registro de cada versión.
2. **La normalización 20–100 no se edita.** Los tramos, el score objetivo y los umbrales
   usan esa escala: cambiarla es una decisión de arquitectura, no un ajuste.

**Qué no se pudo verificar en local:** la reautenticación contra un IdP real
(`max_age=0` y `auth_time`). En local corre el esquema de desarrollo, que la simula; la
lectura del token está aislada en `StepUp`.

## 8bis. Ampliación solicitada — fases 10 a 12"""

patch("PLAN.md", [
    (
        "| **12 — Administración de reglas** | 3 sem | Editar ruleset, palancas de what-if y parámetros de roadmap desde el portal, solo administradores, con borrador, simulación de impacto y publicación auditada | Cambiar un peso desde el portal mueve el score del portafolio, queda auditado y no requiere recompilar |",
        "| **12 — Administración de reglas** ✅ | ENTREGADA | Ruleset, palancas de what-if y parámetros de roadmap editables desde el portal, solo administradores, con borrador, simulación de impacto y publicación auditada | **Cumplido:** cambiar un peso desde el portal mueve el portafolio, queda auditado con su impacto y lo toman las réplicas sin desplegar |",
    ),
    ("## 8bis. Ampliación solicitada — fases 10 a 12", SECTION_85),
    ("### 8bis.3 Fase 12 — Administración de reglas\n", "### 8bis.3 Fase 12 — Administración de reglas (entregada, ver 8.5)\n"),
    (
        "  reescribe la historia: las cargas anteriores conservan la suya.",
        "  reescribe la historia: las cargas anteriores conservan la suya. *(Corregido al\n  entregar: la historia se puntúa con las reglas vigentes; ver 8.5 y ADR 0018.)*",
    ),
    ("# decisiones de arquitectura (0001–0017)", "# decisiones de arquitectura (0001–0018)"),
    ("Modernization.WhatIf/ · Modernization.Roadmap/", "Modernization.WhatIf/ · Modernization.Roadmap/ · Modernization.Rules/"),
])

README_SECTION = """## Administrar las reglas

La pantalla **Rules** ajusta el cálculo sin recompilar ni desplegar
([ADR 0018](docs/adr/0018-las-reglas-se-publican-como-versiones.md)). Solo aparece para
quien tiene el rol `ruleset.admin` y alcance sobre todo el portafolio. El usuario de
desarrollo lo trae por defecto.

1. **Editar** pesos, bandas, puntajes, palancas o parámetros del roadmap. El borrador se
   guarda en la pestaña del navegador.
2. **Simulate impact** recalcula todo el portafolio con las reglas nuevas, sin cambiar nada.
3. **Publish as a new version** pide un motivo y una autenticación de los últimos 10
   minutos. El resto de las réplicas toma la versión nueva en su siguiente comprobación.
4. **Volver atrás:** Versions → Load into draft, y publicar
   ([runbook](docs/runbooks/reglas-defectuosas.md)).

| Variable | Capa | Por defecto | Para qué |
|---|---|---|---|
| `Rules__Directory` | System | `<DataSource__Directory>/.rules` | Dónde guarda las versiones el adaptador de archivos |
| `Rules__RefreshSeconds` | System | 15 | Cada cuánto una réplica comprueba si hay una versión nueva |
| `RulesAdministration__ReauthenticationMinutes` | Channel | 10 | Cuánto vale una autenticación para publicar |
| `RateLimiting__RulesSimulationsPerMinute` | Channel | 10 | Simulaciones por usuario |
| `RateLimiting__RulesPublishesPerWindow` · `RateLimiting__RulesPublishWindowMinutes` | Channel | 5 · 60 | Publicaciones por usuario |

Con el adaptador Dummy las reglas viven en memoria; con base de datos, en la tabla
`rules_version`.

## Prueba de carga
"""

patch("README.md", [
    ("524 tests.", "598 tests."),
    ("| 12 Administración de reglas | pendiente |", "| 12 Administración de reglas | entregada |"),
    ("## Prueba de carga\n", README_SECTION),
])
