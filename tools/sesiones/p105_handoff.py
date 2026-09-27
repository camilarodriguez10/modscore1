from patchlib import patch

patch("HANDOFF.md", [
("""**Fecha:** 2026-09-18 · **Estado:** fases 00 a 13 entregadas; 14 y 15 en curso; dashboard ejecutivo con D1, D2 y D3 entregadas · **Siguiente:** que el negocio confirme las definiciones desde el portal (DD-01 a DD-08) y las reglas P0 del score; terminar la Fase 14 en un entorno de prueba""",
 """**Fecha:** 2026-09-26 · **Estado:** fases 00 a 13 entregadas; 14 y 15 en curso; dashboard ejecutivo con D1, D2 y D3 entregadas; pantalla Code (C1) entregada con datos de demostración · **Siguiente:** que el negocio confirme las definiciones desde el portal (DD-01 a DD-08) y las reglas P0 del score; aprobar la GitHub App y el mapeo de repositorios (GH-01, GH-02); terminar la Fase 14 en un entorno de prueba"""),
("""| Pruebas | **770 .NET** (270 motor · 178 contratos · 322 API) · **58 unitarias** de la SPA · **40 de punta a punta** con Playwright (19 de flujo y 21 de accesibilidad con axe, en claro y oscuro) · ESLint sin errores. Todo en verde el 2026-09-18 |""",
 """| Pantalla Code | C1 entregada el 2026-09-26 (PLAN §8.11, [ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)). `/code`: componentes de cada repositorio contra su fin de vida (endoflife.date, foto versionada con 22 productos y 273 ciclos), bibliotecas con hallazgos, discrepancias con el Currency Score de CMT e índice de riesgo de código con detalle por aplicación. 14 parámetros nuevos (área `code`) editables desde el portal. **Repositorios de demostración** hasta integrar GitHub Enterprise (C2, GH-01 a GH-09) |
| Pruebas | **828 .NET** (309 motor · 183 contratos · 336 API) · **63 unitarias** de la SPA · **49 de punta a punta** con Playwright (24 de flujo y 25 de accesibilidad con axe, en claro y oscuro) · ESLint sin errores. Todo en verde el 2026-09-26 |"""),
("""| Pruebas de la SPA | `npm --prefix src/web run lint` (el CI lo exige) · `npm --prefix src/web test` · `npm --prefix src/web run test:e2e` (40 escenarios, 21 de ellos de accesibilidad) |""",
 """| Pruebas de la SPA | `npm --prefix src/web run lint` (el CI lo exige) · `npm --prefix src/web test` · `npm --prefix src/web run test:e2e` (49 escenarios, 25 de ellos de accesibilidad) |
| Catálogo de fin de vida | `python tools/eol-catalog/build-eol-catalog.py` (o `--from <carpeta>` con los JSON bajados con `curl`), y después `dotnet test tests/Modernization.Scoring.Engine.Tests --filter EndOfLifeCatalog` |"""),
("""- **El impacto construye cada plan una sola vez** (`RulesImpactSimulator.Plan`) y de él saca
  el score, el resumen del roadmap y los indicadores del dashboard.
""",
"""- **El impacto construye cada plan una sola vez** (`RulesImpactSimulator.Plan`) y de él saca
  el score, el resumen del roadmap y los indicadores del dashboard.

## 6quater. La pantalla Code

Pedido del 2026-09-26: analizar el código de las aplicaciones desde GitHub Enterprise, sin
configurar todavía la integración, con una vista de obsolescencia y riesgo por librerías,
software y componentes y una fuente para el fin de vida. Detalle en PLAN §8.11 y en
[ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md).

**Piezas:**

- **Dominio:** `Modernization.Domain/Code/CodeInventory.cs` —`RepositoryInventory`,
  `RepositoryComponent` (producto con el identificador de endoflife.date), `RepositoryDependency`,
  `CodeInventorySet` y el puerto `ICodeInventoryProvider`, con `NoCodeInventoryProvider`—.
  Es la forma que entregará el recolector.
- **Motor:** `EndOfLifeCatalog` (carga y valida `contracts/eol-catalog-v1.json`, cruza versión
  con ciclo y calcula el estado contra una fecha), `CodeModel` y `CodeAnalyzer` (función
  pura), y `CodeRules` dentro de `DashboardRules`, con sus 14 parámetros en
  `AnalyticsCatalog` (área `code`, regla `code.levels`, tipo nuevo `months`).
- **Fuentes:** `SampleCodeInventoryProvider` en `Modernization.Sources/Dummy`: repositorios
  deterministas por EAI (semilla FNV-1a, no `GetHashCode`) a partir del portafolio vigente,
  con caché por versión de carga.
- **Capa System:** `CodeEndpoints` (`GET /system/code` con los filtros de siempre y
  `GET /system/code/applications/{eai}`), `CodeContracts` y el registro en `Program.cs`
  según `CodeInventory:Provider`.
- **Capa Channel:** `GET /api/code` y `GET /api/code/applications/{eai}`, que validan y
  entregan.
- **SPA:** `routes/CodePage.tsx`, `i18n/code.ts`, `codeExplanation` en `i18n/analytics.ts`,
  la sección *Code* de `DefinitionsPage.tsx` y la pestaña *Code* en la navegación.

**Cuidados:**

- **El catálogo se regenera, no se edita a mano:** `python tools/eol-catalog/build-eol-catalog.py`
  baja un JSON por producto de la API de endoflife.date y escribe la foto con la fecha del
  día. Después, `EndOfLifeCatalogTests`, que fija algunas fechas conocidas. En este equipo
  Python no valida el certificado de endoflife.date (`CERTIFICATE_VERIFY_FAILED`) y `curl`
  sí: se bajan los archivos con `curl` y se pasa `--from <carpeta>`. La verificación TLS no
  se desactiva.
- **El estado se evalúa contra hoy (UTC)**, no contra la carga: las pruebas del analizador
  pasan la fecha, las de API comparan con `DateTime.UtcNow`.
- **Un producto que el generador de demostración escribe mal no falla en ningún lado**:
  aparece como *not assessable*. `EveryGeneratedComponentIsInTheCatalogButTheDeliberateGap`
  lo impide; WebSphere falta del catálogo a propósito, para mostrar ese caso.
- **Las versiones «actuales» del generador vencen.** El 2026-09-26 hubo que mover Node 18 y
  20, Spring Boot 3.3 y 3.5, Angular 17 y 19 y Django 4.2 a la lista de viejas: ya estaban
  vencidos. Al regenerar el catálogo conviene mirar la distribución por estado.
- **`Sample` fuera de Development no arranca**, a propósito. Una prueba lo verifica.
- **El índice es una suma sin tope**: una aplicación con muchos repositorios acumula más.
  Si el negocio lo quiere normalizado, es código (GH-07).
"""),
("""- **`"vite" no se reconoce como un comando interno o externo`** es siempre que faltan las""",
 """- **En Playwright, `getByLabel('LOB')` también encuentra el filtro CIO**: el nombre accesible
  de un `select` incluye sus opciones, y una de ellas es «R. Villalobos». Para un filtro,
  `getByRole('combobox', { name: /^LOB/ })`.
- **En el panel del navegador, las capturas de una página larga salen en blanco** después de
  desplazarse (la pantalla Code mide unos 7000 px). No es la página: el contenido se
  verifica con el texto de la página o el árbol de accesibilidad, y la captura se toma
  arriba o en el detalle.
- **`"vite" no se reconoce como un comando interno o externo`** es siempre que faltan las"""),
])
