# Traspaso — Modernization Score Platform

**Fecha:** 2026-09-18 · **Estado:** fases 00 a 13 entregadas; 14 y 15 en curso; dashboard ejecutivo con D1 y D2 entregadas · **Siguiente:** calibrar con el negocio las definiciones provisionales del dashboard, terminar la Fase 14 en un entorno de prueba

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
| Dashboard ejecutivo | D1 y D2 entregadas el 2026-09-18 (PLAN §8.10, [ADR 0024](docs/adr/0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md)). `/dashboard`, primera pestaña: *Portfolio Health & Risk Intelligence*, con cinco indicadores, seis paneles y un detalle común. Lo que la propuesta deja en TBD tiene un default en `contracts/dashboard-v1.json`, marcado como provisional. Cinco filtros nuevos en todas las pantallas: CRE, Region, App Status, Primary Capability y EAI Code. Faltan D3 (diccionario y edición desde el portal) y D4 (fuentes nuevas) |
| Pruebas | **737 .NET** (242 motor · 178 contratos · 317 API) · **47 unitarias** de la SPA · **30 de punta a punta** con Playwright (14 de flujo y 16 de accesibilidad con axe, en claro y oscuro) · ESLint sin errores. Todo en verde el 2026-09-18 |
| Dependencias | Cero paquetes vulnerables (`dotnet list package --vulnerable`), `npm audit` limpio |
| CI | GitHub Actions en verde desde la corrida #5 (`55cf36f`, 2026-09-15), que pasó los 7 jobs: backend, SPA, gitleaks, OSV, CodeQL C# y JS/TS, e imágenes con Trivy v0.74.0 y Checkov. CodeQL sin hallazgos altos. Tras acelerarlo, la corrida #6 (`148c6ce`) tardó 7 min 3 s en lugar de 9 min 54 s; CodeQL C# sigue marcando el total (6 min 58 s). Antes hubo que corregir el lint, CodeQL en repositorio privado (`upload: never`, `actions: read`), la versión de Trivy y el UID de los pods. Para acortarlo: gitleaks y OSV están en `seguridad.yml` y corren en cada push; `ci.yml` se salta los cambios que solo tocan documentación; CodeQL C# compila solo `src/` sin los analizadores de Roslyn; hay caché de NuGet, y el job de imágenes no espera a los demás |
| ASVS L2 | 106 cumple · 11 parciales · 3 pendientes · 10 IdP o N/A. Los parciales dependen de un entorno real |
| Hallazgos | Fase 08: 23 (19 cerrados, H-16 parcial, H-17/H-20/H-21 pendientes). Revisión 09–13: 7 (6 cerrados, 1 aceptado) |
| Git | Repositorio **privado** <https://github.com/camilarodriguez10/modscore1>, rama `main`. Guarda todo lo creado —el `.zip` portable, el estado local de `data/`, los scripts de las sesiones (`tools/sesiones/`) y la copia del plan (`docs/plan/`)— salvo las capturas del BRD y las imágenes. Los commits usan el correo de la cuenta. Cada push se confirma antes |
| Memo de decisiones | <https://claude.ai/artifact/Gp7c9gHv5kTSdGETPfeCi5> (version 1, del 2026-09-16): las once reglas de scoring que el negocio tiene que confirmar, con evidencia y opciones. Fuente de verdad: [docs/scoring-decisions.md](docs/scoring-decisions.md); copia publicada en `docs/plan/scoring-decisions.html` |
| Artefacto del plan | <https://claude.ai/artifact/5cFSpe2puMikBkB3u5HRKd> (versión 19, del 2026-09-18, con el dashboard ejecutivo; copia en `docs/plan/modscore-plan.html`, que hay que reemplazar al republicar; enlace anterior del mismo artefacto: <https://claude.ai/code/artifact/25507d1f-40a6-4e74-8c43-0337dbb63358>) |
| Paquete portable | `artifacts/modscore-portable-win-x64-selfcontained-20260918-0503.zip` (93,3 MB autocontenido, SHA-256 `236832c2a123902b566c7d1a2397bbe175d63ddda26464490fac4abf11e65429`), versionado en el repositorio; se regenera con `tools/portable/build-portable.ps1`. Verificado el 2026-09-18, ya con el dashboard y con `GUIA-PASO-A-PASO.txt` dentro: descomprimido y arrancado con `start.ps1`; los **30 escenarios e2e pasan contra él**. El paquete tiene el límite real de 120 peticiones por minuto por usuario, y la suite completa lo supera en un minuto (la prueba de reglas recibe 429): para correrla, arrancarlo con los límites de `playwright.config.ts` (`RateLimiting__RequestsPerMinute=100000` y los otros tres) en el entorno. Con el límite real pasan en dos tandas. `start.ps1` espera un Enter para detenerse: en segundo plano, darle una entrada que quede abierta. El anterior se borra: cada paquete queda para siempre en la historia del repositorio |

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
| Despliegue previsto | Kubernetes (`deploy/k8s`), imágenes chiseled no root, NetworkPolicy default-deny, mTLS del mesh |

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
| Todo en el repositorio | Paquete portable, estado local de prueba, scripts de las sesiones, copia del plan, `.claude/launch.json` | `artifacts/`, `data/`, `tools/sesiones/`, `docs/plan/` |
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
| Paquete portable | Ejecutables de un solo archivo con la SPA; lanzador local | `tools/portable/` |

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
| ¿Qué tan sano está? | 1 | Mapa de calor capacidad × región, país, CIO o disposición; dona de tramos; tendencia por carga |
| ¿Qué modernizar primero? | 2 | Matriz criticidad (0–3) × oportunidad (0–3), burbuja = Risk Score; top 10 |
| ¿Dónde reducir complejidad? | 3 | Treemap de seis dimensiones (dos sin fuente) y cuatro oportunidades |
| ¿Dónde se concentra el riesgo? | 4 | El mismo mapa midiendo riesgo; top 5; índice de concentración 0–1 |
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

---

## 7. Comandos frecuentes

| Para | Comando |
|---|---|
| Pruebas .NET | `dotnet test ModernizationScore.slnx` |
| Pruebas de la SPA | `npm --prefix src/web run lint` (el CI lo exige) · `npm --prefix src/web test` · `npm --prefix src/web run test:e2e` (30 escenarios, 16 de ellos de accesibilidad) |
| Pruebas contra el paquete portable | Con `start.ps1 -NoBrowser` corriendo, desde `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |
| Tipos de la SPA | `npx --prefix src/web tsc -b` (desde `src/web`: `npx tsc -b`) |
| Vulnerabilidades | `dotnet list ModernizationScore.slnx package --vulnerable --include-transitive` · `npm --prefix src/web audit --audit-level=moderate` |
| Prueba de carga | `dotnet run -c Release --project tools/Modernization.LoadTest -- --duration 10 --concurrency 8` (con system-api en Release; `--scenario <texto>` corre uno solo) · como en un pod de dos núcleos: `powershell -ExecutionPolicy Bypass -File tools/Modernization.LoadTest/medir-en-dos-nucleos.ps1` |
| Paquete portable | `powershell -ExecutionPolicy Bypass -File tools/portable/build-portable.ps1` |
| Respaldo | `powershell -ExecutionPolicy Bypass -File tools/git/backup.ps1 -Message "…" -Verify` |
| Datos dummy | `dotnet run --project tools/Modernization.DummyData -- data/dummy` |
| Levantarlo en otro equipo | Guía paso a paso, para alguien que no conoce el proyecto: [docs/guia-localhost.md](docs/guia-localhost.md). En un equipo que bloquea `.exe` sin firmar, su **camino C**: `dotnet run ... -p:UseAppHost=false` y la Channel sirviendo el sitio con `ASPNETCORE_WEBROOT` |

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
- **El esquema de desarrollo** solo arranca en `Development`. El paquete portable lo usa, y
  por eso escucha solo en `127.0.0.1`.
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
  bloquean programas sin firmar ese `.exe` no abre, igual que los del paquete portable.
  `-p:UseAppHost=false` hace que ejecute `dotnet.exe` con la DLL. La SPA no necesita Node: la
  capa Channel la sirve si `ASPNETCORE_WEBROOT` apunta a una carpeta con `index.html`, y el
  `.zip` portable trae esa carpeta ya compilada en `channel\wwwroot`.

**Proceso y producto:**

- Las capturas del BRD mandan, pero el propio BRD se contradice: la tabla de pesos no
  coincide con su ejemplo trabajado. El ejemplo trabajado y el dashboard pesan más que una
  tabla sin números detrás.
- El cierre de cada fase es: pruebas y escaneo en verde, verificación en vivo, PLAN y README
  al día, artefacto republicado y decir qué no se pudo correr.
- El push a GitHub publica fuera del equipo: se confirma antes, siempre.

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
- confirmar antes de publicar cualquier cosa fuera del equipo, incluido cada push;
- en el repositorio va todo lo creado, salvo las capturas del BRD y las imágenes.
