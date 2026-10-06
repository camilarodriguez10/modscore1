# Traspaso — Modernization Score Platform

**Fecha:** 2026-09-28 · **Estado:** fases 00 a 13 entregadas; 14 y 15 en curso; dashboard ejecutivo con D1, D2 y D3 entregadas; pantalla Code (C1) con proyección a tres años, plan de mejora y evolución por carga, sobre datos de demostración; mapping de columnas previo a la carga y motivos de rechazo; paquete portable retirado · **Siguiente:** que el negocio confirme las definiciones desde el portal (DD-01 a DD-08) y las reglas P0 del score; aprobar la GitHub App y el mapeo de repositorios (GH-01, GH-02); terminar la Fase 14 en un entorno de prueba

Este documento permite que otra persona —o otra sesión— retome el proyecto sin haber
estado en él. No reemplaza al plan ([PLAN.md](PLAN.md)) ni a los ADR: dice qué hay, dónde
está, qué se decidió, qué falta y qué se aprendió en el camino.

---

## 1. Retomar en diez minutos

1. Leer la sección 0 y la sección 1 de [PLAN.md](PLAN.md): qué es el score y cómo se calcula.
2. Leer la sección 0 de [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md) (en inglés): todo lo
   abierto, con prioridad y responsable sugerido.
3. Verificar que el entorno funciona:

   ```bash
   dotnet test ModernizationScore.slnx
   npm --prefix src/web ci --ignore-scripts
   npm --prefix src/web run lint
   npm --prefix src/web test
   npm --prefix src/web run test:e2e
   ```

4. Levantar la aplicación ([README.md](README.md), «Levantar el stack») y abrir
   <http://localhost:5173>. `Sign in` entra con el usuario de desarrollo, que tiene todos los
   permisos.
5. Antes de cambiar una regla de cálculo, leer la sección 4 de este documento.

---

## 2. Estado

| Área | Estado |
|---|---|
| Fases 00–12 | Entregadas: motor, datos dummy, dos capas, SPA, what-if, roadmap, archivos, endurecimiento, base de datos, experiencia de consulta, sitio en inglés, administración de reglas |
| Fase 13 — revisión y remediación | Entregada el 2026-09-14/15 (PLAN §8.6) |
| Fase 15 — autenticación con APIM | **En curso** (PLAN §8.8). Elegida la opción C con A ([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)): la credencial entre capas es conectable, la capa System valida un JWT de APIM y hay un modo `Both` para cambiar sin corte. Falta el emisor real; el cambio está en [su runbook](docs/runbooks/paso-a-apim.md) |
| Fase 14 — verificación de seguridad | **En curso** (PLAN §8.7). Cerrados SEC-06 (evento 4214), SEC-07 (`Uploads:RetainVersions`) y SEC-12 (ADR 0021); SEC-14 aceptado con controles compensatorios; accesibilidad y e2e en el CI. Los once parciales que quedan necesitan cluster, IdP, ingress y gestor de secretos: ver [runbook de verificación](docs/runbooks/verificacion-asvs.md) |
| Las seis preguntas (pantalla 09) | Entregada el 2026-09-16 (PLAN §8.9, [ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md)). Salud, prioridades, complejidad, riesgo, obsolescencia y resiliencia en `/insights`, con los mismos filtros. Ningún dato nuevo hizo falta; lo que mejoraría el esfuerzo y el valor de negocio está en PENDING-ITEMS como DQ-01 a DQ-07 |
| Dashboard ejecutivo | D1, D2 y D3 entregadas el 2026-09-18 (PLAN §8.10, [ADR 0024](docs/adr/0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md), [ADR 0025](docs/adr/0025-las-definiciones-viajan-con-las-reglas.md)). La página de inicio (`/`): *Portfolio Health & Risk Intelligence*, con cinco indicadores, seis paneles y un detalle común; el inventario está en `/portfolio`. Currency Score = `Raw_Tech_Score` de CMT. Las 37 definiciones de las seis preguntas viajan con las reglas y se editan en *Rules → Portfolio analytics*; cada panel explica su cálculo y *Definitions* es el diccionario. Cinco filtros nuevos en todas las pantallas. Falta D4 (fuentes nuevas) |
| Pantalla Code | C1 entregada el 2026-09-26 (PLAN §8.11, [ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)). `/code`: componentes de cada repositorio contra su fin de vida (endoflife.date, foto versionada con 22 productos y 273 ciclos), bibliotecas con hallazgos, discrepancias con el Currency Score de CMT e índice de riesgo de código con detalle por aplicación. 15 parámetros nuevos (área `code`) editables desde el portal. **Repositorios de demostración** hasta integrar GitHub Enterprise (C2, GH-01 a GH-09) |
| Pruebas | **946 .NET** (320 motor · 270 contratos · 356 API) · **78 unitarias** de la SPA · **59 de punta a punta** con Playwright (32 de flujo y 27 de accesibilidad con axe, en claro y oscuro) · ESLint sin errores. Todo en verde el 2026-10-02 |
| Dependencias | Cero paquetes vulnerables (`dotnet list package --vulnerable`), `npm audit` limpio |
| CI | GitHub Actions en verde desde la corrida #5 (`55cf36f`, 2026-09-15), que pasó los 7 jobs: backend, SPA, gitleaks, OSV, CodeQL C# y JS/TS, e imágenes con Trivy v0.74.0 y Checkov. CodeQL sin hallazgos altos. Tras acelerarlo, la corrida #6 (`148c6ce`) tardó 7 min 3 s en lugar de 9 min 54 s; CodeQL C# sigue marcando el total (6 min 58 s). Antes hubo que corregir el lint, CodeQL en repositorio privado (`upload: never`, `actions: read`), la versión de Trivy y el UID de los pods. Para acortarlo: gitleaks y OSV están en `seguridad.yml` y corren en cada push; `ci.yml` se salta los cambios que solo tocan documentación; CodeQL C# compila solo `src/` sin los analizadores de Roslyn; hay caché de NuGet, y el job de imágenes no espera a los demás |
| ASVS L2 | 106 cumple · 11 parciales · 3 pendientes · 10 IdP o N/A. Los parciales dependen de un entorno real |
| Hallazgos | Fase 08: 23 (19 cerrados, H-16 parcial, H-17/H-20/H-21 pendientes). Revisión 09–13: 7 (6 cerrados, 1 aceptado) |
| Git | Repositorio **privado** <https://github.com/camilarodriguez10/modscore1>, rama `main`. Guarda todo lo creado —el estado local de `data/`, los scripts de las sesiones (`tools/sesiones/`) y la copia del plan (`docs/plan/`)— salvo las capturas del BRD y las imágenes. Los commits usan el correo de la cuenta. Desde el 2026-10-05, cada cambio de código se sube a `main` sin preguntar, por pedido de la dueña del repositorio |
| Memo de decisiones | <https://claude.ai/artifact/Gp7c9gHv5kTSdGETPfeCi5> (versión 2, del 2026-09-18, con la pregunta 9 respondida): las once reglas de scoring que el negocio tiene que confirmar, con evidencia y opciones. Fuente de verdad: [docs/scoring-decisions.md](docs/scoring-decisions.md); copia publicada en `docs/plan/scoring-decisions.html` |
| Artefacto del plan | <https://claude.ai/artifact/5cFSpe2puMikBkB3u5HRKd> (versión 24, del 2026-10-02, con la lectura tolerante de los exports reales; copia en `docs/plan/modscore-plan.html`, que hay que reemplazar al republicar; enlace anterior del mismo artefacto: <https://claude.ai/code/artifact/25507d1f-40a6-4e74-8c43-0337dbb63358>) |
| Paquete portable | **Retirado el 2026-09-27** por decisión: la aplicación se levanta desde el código, con comandos para cmd y PowerShell ([README](README.md), «Levantar el stack», y la [guía](docs/guia-localhost.md)). Se borraron `artifacts/*.zip` y `tools/portable/`; la historia de git conserva los `.zip` anteriores (unos 93 MB cada uno). Para un equipo que no deja ejecutar `.exe` propios, el camino B de la guía sirve el sitio compilado (`src\web\dist`) desde la Channel con solo `dotnet.exe` |

---

## 3. El producto en una página

- **Qué calcula.** El Modernization Score v1.3 de cada aplicación, según el BRD "Modernization
  Scoring Tool" y el dashboard Power BI v1.3.
- **Fórmula.** `Score = 20 + (Numerador ÷ Denominador) × 80`. El numerador suma
  `peso × puntaje / 100` y el denominador suma pesos, ambos solo de las métricas **con dato**.
  El score global se calcula sobre métricas planas, no como promedio de pilares.
- **Pilares.** Modernization Risk Vectors 50, Architecture & Maturity 40, Stability 10.
  **18 métricas** en `contracts/ruleset-v1.3.json`; seis tienen peso 0 y solo se muestran.
- **Casos dorados.** EAI 14381 = **79,67** (ejemplo del BRD, desde datos crudos) y EAI 10969
  = **79,61** (dashboard). Si alguno cambia, el modelo dejó de coincidir: investigar antes de
  tocar nada más.
- **Pantallas** (sitio en inglés):
  - Portfolio, con filtros en la URL y descarga CSV;
  - detalle, cálculo, what-if y plan futuro por aplicación;
  - Roadmap por dimensión, con descarga CSV;
  - Insights: las seis preguntas del portafolio en una vista;
  - Data (cargas, linaje y tendencia con marcas de reglas);
  - Rules (editar, simular y publicar);
  - Compare rules (dos versiones o el borrador, con una aplicación métrica por métrica).

---

## 4. Reglas de cálculo: fuente de verdad y cuidados

**Regla del proyecto (2026-09-14):** ante una contradicción entre documentos, **mandan las
34 capturas del BRD y del dashboard**. Están en `docs/brd/capturas/`, fuera del repositorio,
y [docs/brd/README.md](docs/brd/README.md) dice qué regla está en cada una.

| Dónde vive | Qué |
|---|---|
| `contracts/ruleset-v1.3.json` | Pesos, bandas, casos, compuestos, `zeroIsNoData` y notas de trazabilidad. Es la versión de fábrica |
| Almacén de reglas (memoria, `.rules` o tabla `rules_version`) | Versiones publicadas desde el portal. La vigente es la última |
| `MetricExtractors.cs` | Cómo se lee cada valor crudo del snapshot. Es código: no se edita desde el portal |
| `MetricMapper.cs` · `ScoringEngine.cs` | Mapeo a puntaje y normalización |
| PLAN §1 | La explicación, con las tablas de mapeo |

**Decisiones que ya están tomadas y por qué** (no reabrir sin nueva evidencia):

- **Compuestos** con promedio ponderado por conteo, peso 10 cada uno. Lo confirma el ejemplo
  14381 (RFC-01 pide validarlo con el modelo Power BI).
- **Tech Debt con peso 10.** Es el peso del dashboard. **No está verificado
  numéricamente**: ningún caso dorado tiene Tech Debt (RFC-03).
- **Ausencia frente a cero** ([ADR 0003](docs/adr/0003-ausencia-de-dato-frente-a-valor-cero.md),
  [ADR 0019](docs/adr/0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md)):
  - una fila ausente es sin dato;
  - un cero se excluye en Kenna, OSS, RAW Tech y Operational;
  - en Tech Debt y SCA el cero puntúa, porque la tabla del BRD lo nombra.
- **Huecos entre bandas** ([ADR 0004](docs/adr/0004-huecos-entre-bandas-del-brd.md)): el
  motor falla por aplicación en lugar de adivinar.
- **La historia se puntúa con las reglas vigentes**
  ([ADR 0018](docs/adr/0018-las-reglas-se-publican-como-versiones.md)). La tendencia marca
  cuándo cambiaron.

**Decisiones abiertas que cambian scores:** SR-01, SR-03 y SR-07, en
[PENDING-ITEMS.md](docs/PENDING-ITEMS.md). Casi todas se resuelven publicando una versión de
reglas desde el portal, sin desplegar.

**Antes de cambiar una regla en código:**

1. Encontrar la captura que la respalda.
2. Correr los casos dorados.
3. Simular el cambio en Compare rules con el borrador.
4. Actualizar la nota del ruleset y la sección 1 del plan.

---

## 5. Arquitectura y stack

```
Navegador ──cookie de sesión──▶ channel-api (BFF)  ──token de servicio + permisos reenviados──▶ system-api
  SPA React 19                   sesión en servidor, CSRF,                                        reglas, motor, fuentes,
  (mismo origen)                 rate limit, composición,                                         alcance aplicado sobre
                                 exportaciones                                                    los datos, almacén de reglas
```

| Pieza | Tecnología |
|---|---|
| Backend | .NET 10, minimal APIs, `TreatWarningsAsErrors` con todas las reglas de seguridad del analizador, lock files |
| Datos | Adaptadores Dummy · File (CSV por carga) · Database (EF Core 10 + SQLite verificado) |
| SPA | React 19, Vite 7, TypeScript 5.9 estricto, React Router 7, TanStack Query 5, Tailwind 4, SVG propio |
| Pruebas | xUnit (motor, contratos, API por HTTP en ambas capas), Vitest, Playwright con Edge |
| Despliegue previsto | Kubernetes (`deploy/k8s`), imágenes distroless de Azure Linux, no root (chiseled de Ubuntu hasta el 2026-10-02), NetworkPolicy default-deny, mTLS del mesh |

Proyectos clave:

- `src/shared/Modernization.Domain`: modelo canónico y avisos (`NoticeCodes`).
- `Modernization.Scoring.Engine`, `Modernization.WhatIf`, `Modernization.Roadmap`.
- `Modernization.Rules`: editor, comparación, impacto y modelo de versión.
- `Modernization.Security`: alcance, auditoría, cabeceras y JSON acotado.
- `src/system/Modernization.SystemApi`: servicios `PortfolioService`, `RulesService`,
  `PortfolioWarmup` y la ingesta.
- `src/channel/Modernization.ChannelApi`: composers, autenticación, exportaciones.
- `src/web`: SPA.

---

## 6. Qué se hizo en la Fase 13

Pedido del 2026-09-14, trece puntos. Detalle en PLAN §8.6 y en la sección 7 de
[PENDING-ITEMS.md](docs/PENDING-ITEMS.md).

| Punto | Resultado | Dónde mirar |
|---|---|---|
| Plan, handoff | PLAN §1, §6.1, §8, §8.6, §8ter, §9 y §10; este documento | `PLAN.md` |
| Git | Repositorio privado, `.gitignore`, `.gitattributes`, scripts de respaldo que bloquean capturas, imágenes, llaves, `.env` y archivos de más de 100 MB, runbook | `tools/git/`, `docs/runbooks/respaldo-en-git.md` |
| Todo en el repositorio | Estado local de prueba, línea de tiempo dummy, scripts de las sesiones, copia del plan, `.claude/launch.json` | `data/`, `tools/sesiones/`, `docs/plan/` |
| Reglas y documentación | Regla del cero, notas del ruleset, RFC-13 a 17, ADR 0003/0017/0018 corregidos, 18 métricas | ADR 0019, `contracts/ruleset-v1.3.json` |
| Pendientes en inglés | Un archivo con todo lo abierto | `docs/PENDING-ITEMS.md` |
| H-16/17/20/21 al final | Fase 17 | PLAN §8ter |
| ASVS al plan | Fase 14 con cada parcial | PLAN §8ter, `docs/security/asvs-l2.md` |
| Deuda técnica | Listado barato de versiones, limpieza de staging, contrato C#↔TS, carga bajo demanda, lógica del editor extraída, prueba de carga con reglas | `RulesService`, `FileRulesStore`, `ViewModelContractTests`, `src/web/src/rules/draft.ts` |
| Rendimiento | Historia en paralelo y calentamiento; simulación con lado vigente reutilizado | `PortfolioService`, `PortfolioWarmup`, `RulesImpact.cs` |
| Robustez y APIM | Almacén de reglas caído no tumba scores; `IReauthenticationFlow` cierra la sesión anterior; ADR 0020 | `RulesStoreFailureTests`, `ReauthenticationFlowTests` |
| Paquetes desactualizados | Se mantienen si no son vulnerables | PENDING-ITEMS TD-01 |
| Pruebas de la SPA | Vitest y Playwright | `src/web/src/**/*.test.ts`, `src/web/e2e/` |
| Sugerencia 4 | Marcas de reglas en la tendencia; `GET /api/rules/timeline` | `ScoreTrend.tsx` |
| Sugerencia 5 | Vista `/admin/rules/compare` | `RulesComparePage.tsx`, `RulesComparison.cs` |
| Sugerencia 7 | CSV según filtros y aviso de reglas nuevas | `ExportEndpoints.cs`, `RulesChangedNotice.tsx` |

**Medido** (Release, 30 cargas, 8 clientes):

| Escenario | Antes | Después |
|---|---:|---:|
| Historia en frío | 1.655 ms | 8–15 ms con calentamiento (584 ms sin él) |
| Simulación p50, en reposo | 117 ms | 47 ms (p95 66 ms) |

La simulación depende mucho de la carga de la máquina: con la máquina ocupada dio 204 ms.
El 2026-09-16 se midió fijada a **dos núcleos** (TD-06): p95 329 ms con ocho simulaciones a
la vez, bajo el criterio de 500 ms, y las lecturas siguen cerca de 1 ms mientras simulan.
Acotar el paralelismo **empeoró** la simulación sin mejorar las lecturas, así que no se
dejó. Queda repetirlo en el pod real (`medir-en-dos-nucleos.ps1`).

---

## 6bis. Las seis preguntas del portafolio

Pedido del 2026-09-16. Detalle en PLAN §8.9 y en
[ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md).

| Pregunta | Dónde | Con qué |
|---|---|---|
| ¿Qué tan sano está? | `/insights` | Promedio, mediana, reparto por tramo, cobertura de dato, críticas bajo el umbral sano |
| ¿Qué modernizar primero? | ídem | `uplift ÷ semanas`, ponderado por criticidad, usuarios y capacidades |
| ¿Dónde reducir complejidad? | ídem | Capacidad repetida en un país (umbral 2–5), capacidad en varios países, `Eliminate` activas, muchos lenguajes |
| ¿Dónde se concentra el riesgo? | ídem | Por LOB, país, CIO, capacidad: flaws, Kenna, componentes obsoletos, críticas expuestas |
| ¿Qué causa la obsolescencia? | ídem | Puntos perdidos por métrica; el peso sin dato, aparte |
| ¿Dónde faltan defensas? | ídem | DR, monitoreo, score operacional, RTO/RPO |

**Piezas:** `src/shared/Modernization.Insights` (función pura),
`InsightsEndpoints` en la capa System, un paso en `ChannelEndpoints`,
`src/web/src/routes/InsightsPage.tsx` y doce códigos `insights.*` con su frase en
`src/web/src/i18n/notices.ts`.

**Cuidados:**

- El esfuerzo son las semanas del roadmap, que son **por roadmap y no por aplicación**.
  Es un proxy y la pantalla lo dice; DQ-02 lo convertiría en real.
- Los enums de la respuesta viajan como texto (`Roadmap`, `Kind`). No hay
  `JsonStringEnumConverter` registrado en ninguna capa: los contratos los convierten a
  mano, como el resto del API.
- `ViewModelContractTests` cubre los diez contratos nuevos: cualquier propiedad que se
  agregue en C# tiene que aparecer en `src/web/src/api/types.ts`.

---

## 6ter. El dashboard ejecutivo

Pedido del 2026-09-18, a partir de la propuesta *EA Data & Insights – MVP Proposal* y su
mockup (16 capturas, fuera del repositorio; la marca de la empresa no va al sitio).
Detalle en PLAN §8.10 y en
[ADR 0024](docs/adr/0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md).

| Pregunta | Panel | Con qué |
|---|---|---|
| ¿Qué tan sano está? | 1 | Mapa de calor CIO × región, país, capacidad o disposición, con totales; dona de tramos; tendencia por carga |
| ¿Qué modernizar primero? | 2 | Matriz criticidad (0–3) × oportunidad (0–3), burbuja = Risk Score; top 10 |
| ¿Dónde reducir complejidad? | 3 | Treemap de seis dimensiones (dos sin fuente) y cuatro oportunidades |
| ¿Dónde se concentra el riesgo? | 4 | El mismo mapa midiendo riesgo; top 5; índice de concentración 0–1 |

**Filas por CIO y totales (2026-10-06, pedido del negocio).** Los mapas de calor 1 y 4 tienen
una fila por CIO; la capacidad primaria pasó a ser una opción de columna
(`DashboardOptions.Axes = region, country, capability, disposition`; `axis=cio` ya no existe y
el sitio lo trata como `country`). `DashboardHeatmap` trae `RowTotals`, `ColumnTotals` y
`Total`, promediados sobre las aplicaciones y no sobre las celdas. El índice de concentración
se mide ahora sobre las celdas CIO × eje. Totales también en la matriz de prioridad, en
complejidad (aplicaciones distintas), en obsolescencia, en resiliencia y en las donas. El
separador de las claves de celda era un byte NUL literal en `charts.tsx`; ahora es `\u0000`.

**El pie de página afirmaba un filtro que no existe.** Decía «Excludes SaaS, cancelled and
decommissioned applications. Only Common Service and Core Business Applications»: era el pie
del reporte de Power BI, y nada lo aplicaba. Ahora dice que entra todo el EAI Master, y la
decisión quedó en DD-09. Las causas de diferencia con el reporte están en
`docs/runbooks/conciliar-con-power-bi.md`.
| ¿Qué causa la obsolescencia? | 5 | Puntos perdidos agrupados en conceptos; EOS/EOL esperan a TLM |
| ¿Dónde faltan defensas? | 6 | Recuperación y vulnerabilidad operacional; disponibilidad, dependencias y puntos únicos de falla esperan su fuente |

**Piezas:** `contracts/dashboard-v1.json` (embebido en `Modernization.Insights`),
`DashboardRules`, `DashboardModel` y `DashboardAnalyzer` (función pura);
`DashboardEndpoints` en la capa System (`GET /system/dashboard`, con la tendencia de
`PortfolioService.GetDashboardTrendAsync`); `GET /api/dashboard` en la Channel;
`FilterParameters` en las dos capas (`[AsParameters]`), que reemplaza la lista de
parámetros de filtro repetida en cada endpoint; en la SPA,
`routes/DashboardPage.tsx`, `components/dashboard/charts.tsx` y `panels.tsx`, e
`i18n/dashboard.ts`.

**Cuidados:**

- El eje (`axis`) se valida **en la Channel** contra `DashboardOptions.Axes`, además de
  en la System: si solo lo validara la System, su 400 llegaría a la Channel como error y
  saldría como 500.
- Las dimensiones sin fuente viajan con `status: "needs-data"` y números en null. No
  rellenarlas con un cero: un cero es una medición.
- Con los datos dummy los defaults saturan: *duplicate technologies* y *technology
  proliferation* marcan a las 250 aplicaciones, la fila de criticidad «Alta» queda vacía y
  el 77 % tiene huecos de resiliencia. Es calibración pendiente, no un error.
- Cada panel abre el mismo detalle con un `predicate` sobre `applications`: un filtro
  nuevo en un panel no necesita otra llamada al API.

**D3, el mismo día (ADR 0025).** Las definiciones son una cuarta parte de `RulesModel`,
junto a ruleset, palancas y roadmap:

- **Piezas:** `AnalyticsCatalog` (la única lista de parámetros, con rango, decimales y las
  reglas entre ellos) y `AnalyticsDocument` en `Modernization.Insights`;
  `RulesEditor.ApplyAnalytics` y `RulesDiff.CompareAnalytics` en `Modernization.Rules`;
  `StoredRulesVersion.Analytics` y la migración `DefinicionesDeLasSeisPreguntas`; en la
  SPA, `i18n/analytics.ts` (nombres y explicaciones), `components/Explanation.tsx`,
  `routes/DefinitionsPage.tsx` y la pestaña `AnalyticsTab` de `RulesPage.tsx`.
- **Un parámetro nuevo** se agrega en `AnalyticsCatalog.Parameters` (y su campo en
  `DashboardRules`), su nombre en `i18n/analytics.ts` y, si cambia una frase, en la
  explicación. La pantalla de administración lo muestra sola, y la prueba
  `EveryCatalogParameterReadsWhatItWrites` comprueba que su `Get` y su `Set` apunten al
  mismo campo.
- **Las explicaciones se arman con los valores que llegan**, nunca con números escritos en
  la SPA; una prueba de Vitest cambia un umbral y comprueba que la frase cambie.
- **Una versión publicada antes del 2026-09-18** no trae definiciones y usa las de fábrica
  de hoy.
- **El impacto construye cada plan una sola vez** (`RulesImpactSimulator.Plan`) y de él saca
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
  pura), y `CodeRules` dentro de `DashboardRules`, con sus 15 parámetros en
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
- **En el tiempo (2026-09-27):** `CodeOutlook` en `Modernization.Insights` —horizontes a 0,
  1, 2 y 3 años, sugerencias por repositorio, destino de cada actualización (`BestCycle`) y
  el plan del portafolio—; `DashboardTrendPoint` y `ScoreHistoryPoint` llevan además Currency
  y Risk Score por carga (`DashboardAnalyzer.RiskScoreOf`); `DummyTimeline` en
  `Modernization.Sources/Dummy` rebobina el portafolio trimestre a trimestre, y
  `tools/Modernization.DummyData` escribe `data/timeline` con cuatro cargas.

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
- **«Si nada cambia» es literal:** mismas versiones, mismas alertas, ningún commit. La
  proyección no inventa vulnerabilidades ni fechas; un ciclo sin fecha publicada cuenta como
  vigente. Por eso la vigencia del código cae rápido el primer año con los datos de ejemplo:
  .NET 8 y 9, Python 3.10, Spring y Ubuntu 22.04 vencen entre fines de 2026 y mediados de 2027.
- **El destino de una actualización sale del catálogo** (LTS que más dura y fuera de la
  ventana). Si el catálogo se regenera, los destinos cambian solos; las pruebas de
  `EachComponentGoesToTheRightTarget` fijan algunos y hay que revisarlas.
- **La historia del modo Dummy cambió de naturaleza el 2026-09-27:** antes cada trimestre era
  otro portafolio (otra semilla, otros EAI); ahora son las mismas aplicaciones. Las pruebas no
  dependían de lo anterior.

---

## 6quinquies. Mapping de columnas y motivos de rechazo

Del 2026-09-28, por la primera carga con datos reales: el EAI Master quedó en 0 filas y
3.869 rechazadas, sin decir por qué. Detalle y alternativas en el
[ADR 0027](docs/adr/0027-el-mapping-de-columnas-se-decide-antes-de-cargar.md).

**Qué hay:**

- **Motivos de rechazo:** `CsvSourceReader.ReadOutcome.Reasons` agrupa por código, columna y
  tipo esperado (`ReasonTally`), con todas las columnas malas de cada fila; viaja como
  `PublishedFileOutcome.RejectionReasons` → `UploadFileResponse.RejectionReasons` →
  `UploadFileView.RejectionReasons`. Textos y pistas en `i18n/notices.ts`
  (`rejectionReasonText`, `rejectionHint`, `sampleRowsText`).
- **Mapping:** `ColumnMapping` en `Modernization.Sources/Files` resuelve (`Resolve`: nombre,
  alias, manual, recordado, excluido, faltante), valida y reescribe el encabezado (`Rewrite`
  para el adaptador de archivos, `Open` como lector para el de base). `IncomingSourceFile`
  trae `ColumnMap`; `ISnapshotPublisher.GetCurrentColumnMapsAsync` devuelve el de la carga
  vigente (manifiesto `ColumnMaps`; columna `ColumnMaps` en `snapshot_version`).
- **Endpoints:** System `POST /system/ingestion/mapping/preview` y `GET /system/ingestion/mapping`;
  Channel `POST /api/ingestion/mapping/preview` (rol de carga, rate limit de simulación) y
  `GET /api/ingestion/mapping` (alcance completo). La carga acepta `map` junto a `assign`, uno
  por archivo.
- **SPA:** `mapping.ts` (estado, validación, qué se envía), `routes/MappingReview.tsx` en
  Uploads, `routes/MappingPanel.tsx` como pestaña **Mapping** de Data.

**Cuidados:**

- **Con datos generados la carga está apagada**, así que ninguna prueba de punta a punta había
  visto el formulario de carga: el selector de archivos no tenía etiqueta y nadie lo notó.
  `e2e/mapping.spec.ts` simula las tres respuestas de carga con `page.route`; lo que valida el
  servidor lo cubren `ColumnMappingFlowTests` y las de contrato.
- **La migración `MapeoDeColumnas` se escribió a mano** (no hay `dotnet ef` en este equipo):
  el modelo de la migración es el snapshot con la columna nueva. Las pruebas del adaptador de
  base aplican todas las migraciones sobre SQLite, así que un error ahí se ve.
- **Solo se envía lo decidido:** la SPA manda en el mapping los campos elegidos a mano, los
  recordados y los excluidos, no lo reconocido por nombre o alias; así lo registrado distingue
  una decisión de un reconocimiento.
- **Números con los dos separadores:** el último es el decimal (`1.234,5` = 1234,5). Antes se
  quitaban las comas y quedaba 1,2345 sin aviso.

**Cargas grandes (2026-10-05, [ADR 0030](docs/adr/0030-una-carga-grande-cabe-en-el-pod.md)).**
El export real de SCA pesa 73 MB y el tope era 64 MB por archivo. Antes de subirlo se midió:
con la System limitada como el pod (`-e DOTNET_GCHeapHardLimit=30000000 -e DOTNET_GCHeapCount=1`
en el `dotnet run`), un SCA sintético de 350 mil filas terminaba en `OutOfMemoryException`.
`CsvSourceReader.Row.Values` es ahora un arreglo sobre un índice de columnas compartido
(`RowValues`), con `CacheFields` de CsvHelper; con eso entran 350 mil filas (pico de 675 MB) y
490 mil (815 MB). Tope nuevo: **128 MB por archivo** en `PublishLimits` y `ChannelLimits`
(una prueba exige que coincidan); 256 MB por carga y 500 mil filas, igual. Para medir de nuevo:
generar el SCA replicando las filas del dummy, servirlo junto al sitio con `--webroot` y subirlo
con `fetch` desde la página, con el token CSRF de la cookie; el pico, con `Get-Process`. La
segunda lectura al publicar queda en TD-17.

**Export real de Veracode SCA (2026-10-05).** Con 128 MB entró, pero se rechazaba por
`SVR_DSC` y `FND_STT`: el export trae 28 columnas y ocho campos con otro nombre. Alias nuevos
en el contrato: `SVR_DSCR`, `FND_STTS_DSCR`, `FND_AGE_DSCR`, `SCAN_TYP_DSCR`,
`ROPEN_FND_DSCR`, `CVSSV3_SCOR`, `LIB_VND_NM` y `ACCSBLTY_DSCR` (`VeracodeExportTests`). Valores reales confirmados el 2026-10-05: «1 - Critical» a «4 - Low» (con espacios) y «Open»; se cuentan por la palabra, y una prueba sube el export y verifica los conteos por aplicación. El
cálculo solo usa EAI, severidad y estado, y compara texto: severidades «1-Critical»,
«2-High»… y estado «Open», como describe el BRD para este mismo archivo. Si los conteos de SCA
salen en cero en aplicaciones escaneadas, lo primero es revisar esos valores.

Después se rechazó por una celda de más de 4 096 caracteres en la fila 5624: el export trae
texto libre que no se usa (lo más probable, la descripción de la vulnerabilidad, `SUM_DSCR`; el
mensaje nuevo lo confirma). El tope por campo
pasa a **65 536** (`CsvSourceReader.MaxFieldLength`): sigue cortando una comilla sin cerrar,
que se come las filas siguientes, o un archivo fabricado. El mensaje dice ahora la columna
(`csv.fieldTooLong` con `column`, del índice `Parser.Count` de CsvHelper) para buscar la celda
sin compartir el archivo. Verificado en vivo: una descripción de 10 mil caracteres entra; una
de 70 mil se rechaza con `SUM_DSCR` y su fila.

**Recalcular con lo que pasó (2026-10-05).** Con los exports reales del 2026-10-05, la carga se
rechazaba entera por dos archivos: uno de contenedores OCP (`P65_CONTAINERS_OCP_Weekly_DETAIL`,
de la métrica *Containerized*, con peso 0 en el BRD v1.3 y sin fuente en el contrato) y el SCA
(todavía con la versión de 4 096). La System sigue publicando todo o nada. En la SPA,
`planLeaveOut` (`src/web/src/leaveOut.ts`) arma, desde el veredicto por archivo y lo último que
se envió, la misma carga sin los archivos con error, con su fuente y su mapping alineados, solo
si el EAI Master pasó; la tarjeta *Recalculate with the files that passed* la muestra y pide
confirmar. Verificado en vivo con la System real y el sitio compilado: con los nombres de los
archivos reales, la carga sin ellos publicó 250 aplicaciones con peso disponible 30 (solo EAI
y deuda técnica). Pruebas: `leaveOut.test.ts` y una de punta a punta en claro y oscuro.

**Lectura tolerante (2026-10-02, [ADR 0028](docs/adr/0028-la-lectura-tolera-los-exports-reales.md)).**
Con el encabezado real del EAI Master, exportado de Power BI:

- **Un valor ilegible en un campo opcional queda sin dato y la fila entra**
  (`ReadOutcome.IgnoredValues` → `UploadFileView.IgnoredValues`, *Values loaded as empty*).
  Solo se rechaza la fila sin clave, con un campo obligatorio ilegible o con la clave repetida.
- `ColumnParser`: marcadores de «sin dato» como vacío, números regionales con
  `TryParseNumber` (un entero acepta decimales en cero; antes «650,00» daba 65000 sin aviso),
  `VERDADERO`/`FALSO`, más formatos de fecha y el tipo `code` para identificadores
  (`NormalizeCode`).
- `ColumnSchema.MatchRank` / `BestIndexIn`: nombre exacto, alias en orden y después prefijos de
  resumen («Suma de», «Sum of»…). Lo usan el lector y la revisión del mapping.
- `SourceSchema.UniqueKey` en el EAI Master: queda la última fila por EAI, las demás como
  `row.duplicateKey`. Antes dos filas con el mismo EAI rompían el adaptador de base.
- Sin APP_ID en el maestro, `SnapshotAssembler` une la deuda técnica por el EAI_Code: en la
  organización son el mismo código (confirmado el 2026-10-02). Con la columna, manda la columna;
  los datos de ejemplo tienen APP_ID distintos del EAI. Un primer cruce por el APP_ID del archivo
  de SCA se reemplazó el mismo día.
- `PowerBiExportUploadTests` carga el maestro de ejemplo convertido al formato de Power BI y
  exige que la 14381 siga en 79,67.

**Cuidado:** el Bash de este equipo reduce las barras invertidas dobles de los scripts de
Python en heredoc, así que una secuencia de escape que debía llegar escrita al archivo termina
convertida: un parche que escribe código C# con un salto de línea escapado deja un literal
partido. Para esas ediciones, el editor o `chr(92)`.

---

## 7. Comandos frecuentes

| Para | Comando |
|---|---|
| Pruebas .NET | `dotnet test ModernizationScore.slnx` |
| Pruebas de la SPA | `npm --prefix src/web run lint` (el CI lo exige) · `npm --prefix src/web test` · `npm --prefix src/web run test:e2e` (56 escenarios, 27 de ellos de accesibilidad) |
| Catálogo de fin de vida | `python tools/eol-catalog/build-eol-catalog.py` (o `--from <carpeta>` con los JSON bajados con `curl`), y después `dotnet test tests/Modernization.Scoring.Engine.Tests --filter EndOfLifeCatalog` |
| Pruebas contra un sitio ya arrancado | Con la Channel sirviendo `src/web/dist` en el 5100, desde `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |
| Tipos de la SPA | `npx --prefix src/web tsc -b` (desde `src/web`: `npx tsc -b`) |
| Vulnerabilidades | `dotnet list ModernizationScore.slnx package --vulnerable --include-transitive` · `npm --prefix src/web audit --audit-level=moderate` |
| Prueba de carga | `dotnet run -c Release --project tools/Modernization.LoadTest -- --duration 10 --concurrency 8` (con system-api en Release; `--scenario <texto>` corre uno solo) · como en un pod de dos núcleos: `powershell -ExecutionPolicy Bypass -File tools/Modernization.LoadTest/medir-en-dos-nucleos.ps1` |
| Respaldo | `powershell -ExecutionPolicy Bypass -File tools/git/backup.ps1 -Message "…" -Verify` |
| Datos dummy | `dotnet run --project tools/Modernization.DummyData -- data/dummy` |
| Levantarlo desde la app de Claude | `.claude/launch.json`: `system`, `channel` y `web`, en ese orden y esperando a que la System responda antes de arrancar la Channel. La `system` corre con el adaptador de archivos sobre `data/local` (ruta relativa a la carpeta del proyecto, `../../../data/local`), así que Data acepta cargas; las pruebas e2e siguen con Dummy |
| Levantarlo en otro equipo | Guía paso a paso, para alguien que no conoce el proyecto: [docs/guia-localhost.md](docs/guia-localhost.md). En un equipo que bloquea `.exe` sin firmar, su **camino C**: `dotnet run ... -p:UseAppHost=false -- --environment Development --urls ... --webroot ...`, con la Channel sirviendo el sitio. Todos los comandos pasan el entorno y el puerto **como argumentos**, no como variables: la misma línea sirve en cmd y en PowerShell. La System arranca con `--DataSource:Provider=File --DataSource:Directory=C:\modscore1\data\local`: la carpeta la ignora git y la primera carga son los ocho CSV de `data/dummy` |

---

## 8. Seguridad: lo que no hay que romper

- **El navegador nunca recibe tokens** (BFF). La cookie es `__Host-`, `HttpOnly`, `Secure`,
  `SameSite=Strict`, con sesión en el servidor.
- **CSRF de doble envío en toda mutación.** En las pruebas de API, un POST necesita
  `CreateMutatingClientAsync()`.
- **El alcance se aplica en la capa System, sobre los datos.** Ninguna lectura del portafolio
  compila sin `PortfolioScope` ([ADR 0012](docs/adr/0012-el-alcance-se-aplica-sobre-los-datos.md)).
  Un filtro solo acota.
- **Administrar reglas:**
  - rol `ruleset.admin` con alcance completo;
  - publicar exige autenticación de los últimos 10 minutos;
  - la capa System vuelve a exigir los permisos reenviados;
  - JSON acotado a 128 KB;
  - límites de frecuencia;
  - auditoría 4211 y 4212.
- **El esquema de desarrollo** solo arranca en `Development`, y los comandos de arranque
  escuchan en `localhost`: no sirve para nada fuera de ese equipo.
- **El backend no redacta frases:** devuelve `{ code, args }` y la SPA las arma
  ([ADR 0017](docs/adr/0017-el-backend-no-redacta-frases.md)). Un código nuevo sin frase en
  `notices.ts` hace fallar una prueba.
- **Sin librerías con vulnerabilidades.** Una dependencia nueva debe entrar limpia; las
  desactualizadas sin vulnerabilidades se mantienen.
- **Nada del BRD al repositorio** sin decisión explícita. Los scripts de respaldo lo bloquean.

---

## 9. Lecciones aprendidas

**Herramientas y entorno (Windows, Git Bash y PowerShell):**

- Un heredoc de Bash con comillas simples dentro del contenido falla ("unexpected EOF").
  Conviene escribir el script con la herramienta de archivos y ejecutarlo; los parches de la
  Fase 11 y 12 se hicieron así.
- `cd` dentro de un comando de Bash cambia el directorio de trabajo de la sesión. Usar rutas
  absolutas; la ruta del proyecto tiene espacios.
- Un `dotnet run` vivo bloquea las DLL. Antes de compilar, detenerlo con
  `Get-Process Modernization.SystemApi | Stop-Process`.
- `dotnet test` sale en español en este equipo: el resumen dice `Correctas!` o `Con error!`.
  Con `-clp:ErrorsOnly` el resumen no se imprime y parece que no pasó nada. Guardar la salida
  a un archivo y buscar ahí.
- No compilar en paralelo `dotnet test` y `dotnet publish -r win-x64`: comparten la
  restauración (`project.assets.json`).
- PowerShell 5.1 corre sobre .NET Framework: `RandomNumberGenerator.Fill` no existe, hay que
  usar `RandomNumberGenerator.Create().GetBytes()`.
- El panel del navegador integrado puede estar oculto: las capturas fallan y hay que usar
  JavaScript o `form_input`. Para pruebas repetibles, Playwright con el Edge instalado no
  descarga navegadores.
- Para probar `start.ps1` sin intervención: `(sleep 600; echo) | powershell -File start.ps1 -NoBrowser`.
  El `echo` equivale a presionar Enter y deja que el script detenga las capas y borre los
  secretos; para terminar antes, matar el `sleep`.
- Este equipo tiene Docker 29, pero no `gh` ni `jq`. En Git Bash, `MSYS_NO_PATHCONV=1` evita
  que rutas como `/repo` o `/var/run/docker.sock` se conviertan en rutas de Windows al
  llamar a `docker run`.
- Fijar una acción por SHA no fija la herramienta que instala: `trivy-action` v0.36.0 baja
  Trivy v0.70.0, que tiene avisos de seguridad posteriores. Hay que fijar también su
  `version`.
- `sed -i` sobre un archivo con finales de línea CRLF lo deja en LF y el diff se vuelve el
  archivo entero. Revisar `git diff --numstat` después de editar con `sed`; los scripts de
  parche en Python conservan los finales de línea a propósito.
- En un repositorio privado, la acción de CodeQL necesita `actions: read` aunque no suba
  resultados. Sin ese permiso, `analyze` falla con "Resource not accessible by integration".
- Una captura de Actions puede ser de una corrida anterior al último push. Antes de corregir,
  comparar las líneas que muestra el log con el archivo del commit.

**.NET y analizadores:**

- **CA1873** (argumento costoso en un log) no reconoce la guarda `IsEnabled` con
  `LoggerMessage`: calcular el valor en una variable local.
- **CA1826** pide indexar en lugar de `FirstOrDefault()` sobre listas.
- **CA1068** exige que el `CancellationToken` sea el último parámetro. Para agregar un
  parámetro opcional, usar una sobrecarga.
- **NETSDK1152:** la capa Channel referencia la System por sus contratos, y sus
  `appsettings` chocaban al publicar. Se descartan en `DropReferencedSystemSettings`. Nunca
  se había visto porque las imágenes no se habían construido.
- Publicar con `-r win-x64` agrega a los lock files `Microsoft.NET.ILLink.Tasks` y una sección
  `net10.0/win-x64`; con eso fallaría la restauración del CI en modo bloqueado. Desde el
  2026-09-15, `build-portable.ps1` guarda los lock files antes de publicar y los devuelve al
  terminar.

**Pruebas:**

- En Vitest, un `Response` simulado solo se puede leer una vez: crear uno nuevo por llamada.
- `exactOptionalPropertyTypes` obliga a escribir `| undefined` en props opcionales que
  reciben un valor posiblemente indefinido.
- Una prueba de carga en una máquina ocupada da números que no sirven para comparar. Medir
  en reposo y repetir.
- **Medir antes de optimizar, también lo «defensivo».** TD-06 proponía acotar el
  paralelismo de la simulación para no ahogar al resto. Medido en dos núcleos, no ahogaba a
  nadie y el tope la hacía más lenta: se revirtió. En una máquina de 16 núcleos no se ve
  ninguna contención; hay que fijar el proceso a pocos núcleos (afinidad y
  `DOTNET_PROCESSOR_COUNT`) para parecerse a un pod.
- Un barrido que pasa sin leer nada no prueba nada: el de idioma exige 200 en los caminos
  felices, y el colector de CSV tiene sus propias pruebas.
- Lo mismo con la prueba de carga: con 100 000 peticiones por minuto de límite, las lecturas
  cacheadas agotan la ventana en diez segundos y los escenarios siguientes «miden» 429 a
  0,4 ms. Un resultado con 429 no cuenta.
- Una prueba de contrato entre C# y TypeScript da la garantía de un cliente generado sin
  sumar herramientas a la cadena de suministro.
- Un `DelegatingHandler` nuevo hay que agregarlo a **todos** los clientes HTTP, no solo al
  principal: el de cargas se quedó sin credencial y lo encontraron las pruebas, no el
  compilador.
- En Playwright, esperar «el enlace Portfolio» falla en la pantalla de cálculo, que tiene
  dos: el del encabezado y el de su navegación. La carrera aparece y desaparece según lo
  rápido que renderice. El ayudante apunta al del encabezado (`getByRole('banner')`).
- axe encuentra lo que la revisión a ojo no: un color atenuado a 4,42:1 y colores de estado
  usados como letra a 1,65:1. Los colores de marca y los de texto son dos cosas distintas y
  conviene que sean dos tokens distintos.
- **Playwright reutiliza los servidores que ya estén corriendo** (`reuseExistingServer`
  fuera del CI). Si se levantaron a mano para mirar la pantalla, no tienen los límites de
  tasa holgados de la configuración, y la suite falla al azar —cada corrida en una prueba
  distinta—. Apagarlos antes de correr las e2e.
- Atenuar con `opacity` un texto de estado lo deja bajo 4,5:1: los tokens `*-text` sobre su
  `*-wash` pasan con lo justo (4,6:1). En el dashboard lo encontró axe en tres lugares.
- Un enlace dentro de un párrafo tiene que estar **subrayado siempre**, no solo al pasar el
  mouse: con el color como única diferencia, axe lo marca (`link-in-text-block`, 1,1:1
  contra el texto que lo rodea).
- **Leer la escala antes de escribir un umbral.** Insights contaba como gestión débil a
  Kenna < 50, pero Kenna va de 0 a 1000 y más alto es peor: sumaba a las mejores
  aplicaciones. Lo encontró la revisión al pasar el umbral a parámetro, no una prueba,
  porque la prueba tenía el mismo supuesto al revés.
- `dotnet ef migrations add --no-build` usa el ensamblado ya compilado: si el modelo cambió y
  no se compiló, genera una migración **vacía** sin avisar. Generarla compilando, y leerla.
- `npx <paquete>` con un paquete que el proyecto no tiene **lo descarga y lo ejecuta**. El
  2026-09-18 se corrió `npx prettier` y reformateó cuatro archivos con otro estilo; se
  restauraron. El proyecto no tiene formateador: el estilo es a mano (comillas simples,
  unas 120 columnas). No usar `npx` fuera de las herramientas de `package.json`.
- **No compilar la System y la Channel a la vez.** La Channel referencia el proyecto de la
  System; si las dos arrancan juntas con `dotnet run`, ambas escriben
  `Modernization.SystemApi.dll` en `obj/` y una falla con `CS2012` (archivo en uso, a veces
  por Defender). Arrancar la System, esperar a que responda y después la Channel.
- **Con `NoDefaultCurrentDirectoryInExePath=1`**, una política que algunas empresas activan
  (y que tiene el equipo de desarrollo), `cmd` no ejecuta nada de la carpeta actual si no se
  escribe `.\`: `start.cmd` y `Modernization.SystemApi.exe` responden «no se reconoce como un
  comando interno o externo». Los comandos documentados llevan siempre `.\`, que funciona con y
  sin la política.
- La verificación local tiene que incluir `npm run lint`, porque el CI lo exige. En la Fase 13
  no se corrió y se escapó un BOM literal en una expresión regular de `e2e/support.ts`
  (`no-irregular-whitespace`); se corrigió escribiendo `\uFEFF`.

**Arrancar en otro equipo:**

- **Los comandos de arranque del README y de la guía nunca se habían corrido tal como
  estaban escritos.** Aquí siempre se pasaban `ASPNETCORE_ENVIRONMENT` y `ASPNETCORE_URLS`
  (la configuración de Playwright los pone), y no hay `launchSettings.json`. En un equipo
  limpio, `dotnet run` arranca en Production, los guardas de despliegue lo detienen
  (`AllowedHosts`) y, sin URL, escucharía en el 5000. Lo encontró una persona en otro equipo
  el 2026-09-17. Un comando documentado se prueba en una terminal sin variables.
- `dotnet run` ejecuta un `.exe` propio (el *apphost*) que crea en `bin`. En equipos que
  bloquean programas sin firmar ese `.exe` no abre. `-p:UseAppHost=false` hace que ejecute
  `dotnet.exe` con la DLL. La SPA no necesita Vite: la capa Channel la sirve si `--webroot`
  (o `ASPNETCORE_WEBROOT`) apunta a una carpeta con `index.html`, la que deja
  `npm run build` en `src\web\dist` (se puede compilar en otro equipo y copiar). Desde el
  2026-10-05 el sitio compilado **se versiona** en `sitio-compilado/`
  ([ADR 0029](docs/adr/0029-el-sitio-compilado-viaja-con-el-codigo.md)), y el camino C de la
  guía levanta la aplicación con solo el .NET SDK y el ZIP de GitHub, sin Node ni Vite. Antes
  era un artefacto del CI; en el equipo de la empresa no se pudo descargar, el proxy bloqueó
  `brace-expansion@1.1.21` (403, versiones recientes) y no se pueden pasar archivos.
- **Quien cambie `src/web` regenera `sitio-compilado/`** con `npm run build:sitio-compilado`
  (desde `src/web`) y lo versiona en el mismo commit. El job SPA compara byte a byte el build
  con la carpeta y falla si difiere: un sitio más viejo que el código no da ningún error,
  solo faltan pantallas (pasó con un `dist` del día anterior al probar el camino C). El build
  es reproducible entre Windows y Linux; si dejara de serlo, se corrige el build.
- **Sin `--DataSource:Provider`, la System arranca con Dummy y Data no acepta cargas**
  (*The active adapter is 'Dummy'*). Los comandos de arranque documentados no lo pasaban, y
  quien usa la aplicación en otro equipo no podía subir archivos (2026-09-21). Desde entonces
  la ventana 1 lleva el adaptador de archivos sobre `data\local`, con ruta absoluta —`dotnet
  run --project` arranca en la carpeta del proyecto— y excluida de git, porque ahí pueden
  terminar datos reales. `data/history` sigue versionada con dos cargas de la fase 8: no es
  carpeta de trabajo.
- **En Playwright, `getByLabel('LOB')` también encuentra el filtro CIO**: el nombre accesible
  de un `select` incluye sus opciones, y una de ellas es «R. Villalobos». Para un filtro,
  `getByRole('combobox', { name: /^LOB/ })`.
- **En el panel del navegador, las capturas de una página larga salen en blanco** después de
  desplazarse (la pantalla Code mide unos 7000 px). No es la página: el contenido se
  verifica con el texto de la página o el árbol de accesibilidad, y la captura se toma
  arriba o en el detalle.
- **`"vite" no se reconoce como un comando interno o externo`** es siempre que faltan las
  dependencias del sitio: `npm run dev` busca `vite` en `src/web/node_modules/.bin`. Tres
  formas de llegar ahí, todas vistas: no correr la instalación, correrla en PowerShell con
  `npm` (resuelve a `npm.ps1` y la política la bloquea) o correrla en otra carpeta. Los
  comandos documentados usan `npm --prefix src\web ci --ignore-scripts` —exactamente el lock
  auditado, como el CI— y comprueban después que exista `vite.cmd`; en PowerShell, `npm.cmd`.
  Vite 7 pide Node 22.12+ o 20.19+, no «22 o superior». Lo reportó quien usa la aplicación en
  otro equipo el 2026-09-21.
- **Un bloque de `cmd` pegado en PowerShell no define variables de entorno.** En PowerShell,
  `set "ASPNETCORE_ENVIRONMENT=Development"` es `Set-Variable`: crea una variable de PowerShell,
  no da error, y el programa arranca como producción (`'AllowedHosts' debe listar los hosts del
  servicio`, `DeploymentGuards.cs:line 32`, `Program.cs:line 214`). La Terminal de Windows 11
  abre PowerShell por defecto, así que es el error más probable al seguir una guía de cmd. Lo
  reportó quien usa la aplicación en otro equipo el 2026-09-21. Desde entonces los comandos
  documentados pasan el entorno, el puerto y el resto como argumentos de la aplicación
  (`--environment Development --urls ... --SystemApi:BaseUrl=... --webroot ...`, después de
  `--` en un `dotnet run`), que se escriben igual en cmd, PowerShell y Git Bash.

**Proceso y producto:**

- Las capturas del BRD mandan, pero el propio BRD se contradice: la tabla de pesos no
  coincide con su ejemplo trabajado. El ejemplo trabajado y el dashboard pesan más que una
  tabla sin números detrás.
- El cierre de cada fase es: pruebas y escaneo en verde, verificación en vivo, PLAN y README
  al día, artefacto republicado y decir qué no se pudo correr.
- Cada cambio de código se sube a GitHub (`main`) al terminarlo, sin preguntar: lo pidió la
  dueña del repositorio el 2026-10-05. Lo demás que publique fuera del equipo se confirma antes.

---

## 10. Pendientes

Todos, con contexto y criterio de aceptación, en [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md).
Lo que bloquea producción (P0):

| ID | Qué |
|---|---|
| SR-01 · SR-03 · SR-07 | Confirmar compuestos, peso de Tech Debt y la regla del cero con el negocio y el modelo Power BI |
| PA-01 | Destino de despliegue y motor de base |
| PA-02 · PA-03 | Autenticación con APIM (opción A, B o C del ADR 0020) y claim de alcance |
| SEC-19 | Reautenticación verificada contra el proveedor real |
| SEC-20 | ZAP contra staging (depende de PA-01). El resto del pipeline está en verde desde la corrida #5 |

SEC-21 quedó resuelto el 2026-09-15 (repositorio privado, capturas e imágenes fuera, correo
de la cuenta). OP-04 sigue abierto como P1: protección de la rama `main`, Renovate y
escaneo de secretos en GitHub.

Orden de las fases siguientes: **14** verificación de seguridad (ASVS) → **15** APIM →
**16** escala y operación → **17** H-16, H-17, H-20 y H-21, al final por decisión.

---

## 11. Contexto para una sesión de Claude Code

La memoria del proyecto está en
`C:\Users\kmiro\.claude\projects\C--Users-kmiro-Downloads-mod-score\memory\`:

- idioma y reglas del proyecto;
- cierre de fase;
- artefacto del plan;
- prioridades del plan;
- migración a APIM;
- política de paquetes desactualizados;
- repositorio git;
- estado tras la Fase 14.

Se carga sola en cada sesión.

Preferencias de quien lleva el proyecto:

- conversación y documentación en español, sitio en inglés;
- nunca librerías con vulnerabilidades;
- arquitectura de microservicios con la seguridad al extremo;
- subir a GitHub cada cambio de código sin preguntar (desde el 2026-10-05); confirmar antes de
  publicar cualquier otra cosa fuera del equipo;
- en el repositorio va todo lo creado, salvo las capturas del BRD y las imágenes.
