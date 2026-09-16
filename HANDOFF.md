# Traspaso — Modernization Score Platform

**Fecha:** 2026-09-15 · **Estado:** fases 00 a 13 entregadas; Fase 14 en curso · **Siguiente:** terminar la Fase 14 en un entorno de prueba y decisiones de negocio en paralelo

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
| Pruebas | **694 .NET** (214 motor · 178 contratos · 302 API) · **39 unitarias** de la SPA · **22 de punta a punta** con Playwright (10 de flujo y 12 de accesibilidad con axe, en claro y oscuro) · ESLint sin errores. Todo en verde el 2026-09-16 |
| Dependencias | Cero paquetes vulnerables (`dotnet list package --vulnerable`), `npm audit` limpio |
| CI | GitHub Actions en verde desde la corrida #5 (`55cf36f`, 2026-09-15), que pasó los 7 jobs: backend, SPA, gitleaks, OSV, CodeQL C# y JS/TS, e imágenes con Trivy v0.74.0 y Checkov. CodeQL sin hallazgos altos. Tras acelerarlo, la corrida #6 (`148c6ce`) tardó 7 min 3 s en lugar de 9 min 54 s; CodeQL C# sigue marcando el total (6 min 58 s). Antes hubo que corregir el lint, CodeQL en repositorio privado (`upload: never`, `actions: read`), la versión de Trivy y el UID de los pods. Para acortarlo: gitleaks y OSV están en `seguridad.yml` y corren en cada push; `ci.yml` se salta los cambios que solo tocan documentación; CodeQL C# compila solo `src/` sin los analizadores de Roslyn; hay caché de NuGet, y el job de imágenes no espera a los demás |
| ASVS L2 | 106 cumple · 11 parciales · 3 pendientes · 10 IdP o N/A. Los parciales dependen de un entorno real |
| Hallazgos | Fase 08: 23 (19 cerrados, H-16 parcial, H-17/H-20/H-21 pendientes). Revisión 09–13: 7 (6 cerrados, 1 aceptado) |
| Git | Repositorio **privado** <https://github.com/camilarodriguez10/modscore1>, rama `main`. Guarda todo lo creado —el `.zip` portable, el estado local de `data/`, los scripts de las sesiones (`tools/sesiones/`) y la copia del plan (`docs/plan/`)— salvo las capturas del BRD y las imágenes. Los commits usan el correo de la cuenta. Cada push se confirma antes |
| Memo de decisiones | <https://claude.ai/artifact/Gp7c9gHv5kTSdGETPfeCi5> (version 1, del 2026-09-16): las once reglas de scoring que el negocio tiene que confirmar, con evidencia y opciones. Fuente de verdad: [docs/scoring-decisions.md](docs/scoring-decisions.md); copia publicada en `docs/plan/scoring-decisions.html` |
| Artefacto del plan | <https://claude.ai/artifact/5cFSpe2puMikBkB3u5HRKd> (versión 18, del 2026-09-16, con la pantalla 09 y el estado de la Fase 15; copia en `docs/plan/modscore-plan.html`, que hay que reemplazar al republicar; enlace anterior del mismo artefacto: <https://claude.ai/code/artifact/25507d1f-40a6-4e74-8c43-0337dbb63358>) |
| Paquete portable | `artifacts/modscore-portable-win-x64-selfcontained-20260916-1524.zip` (93,2 MB autocontenido, SHA-256 `f4b322f4eb772003b53a990b280a3280fb2000be1a1206cb0030550c4bc31b0a`), versionado en el repositorio; se regenera con `tools/portable/build-portable.ps1`. Verificado el 2026-09-16, ya con la pantalla 09: descomprimido y arrancado con `start.ps1`; las dos capas escuchan solo en `127.0.0.1`, con CSP estricta; los **22 escenarios e2e pasan contra él**. El anterior se borra: cada paquete queda para siempre en la historia del repositorio. **El `.zip` versionado se generó antes de que el paquete llevara `GUIA-PASO-A-PASO.txt`**; el próximo build ya la incluye, y hasta entonces la guía está en [docs/guia-localhost.md](docs/guia-localhost.md) |

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

La simulación depende mucho de la carga de la máquina: con la máquina ocupada dio 204 ms
(TD-06).

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

## 7. Comandos frecuentes

| Para | Comando |
|---|---|
| Pruebas .NET | `dotnet test ModernizationScore.slnx` |
| Pruebas de la SPA | `npm --prefix src/web run lint` (el CI lo exige) · `npm --prefix src/web test` · `npm --prefix src/web run test:e2e` (22 escenarios, 12 de ellos de accesibilidad) |
| Pruebas contra el paquete portable | Con `start.ps1 -NoBrowser` corriendo, desde `src/web`: `MODSCORE_E2E_BASE_URL=http://localhost:5100 npx playwright test` |
| Tipos de la SPA | `npx --prefix src/web tsc -b` (desde `src/web`: `npx tsc -b`) |
| Vulnerabilidades | `dotnet list ModernizationScore.slnx package --vulnerable --include-transitive` · `npm --prefix src/web audit --audit-level=moderate` |
| Prueba de carga | `dotnet run -c Release --project tools/Modernization.LoadTest -- --duration 10 --concurrency 8` (con system-api en Release) |
| Paquete portable | `powershell -ExecutionPolicy Bypass -File tools/portable/build-portable.ps1` |
| Respaldo | `powershell -ExecutionPolicy Bypass -File tools/git/backup.ps1 -Message "…" -Verify` |
| Datos dummy | `dotnet run --project tools/Modernization.DummyData -- data/dummy` |
| Levantarlo en otro equipo | Guía paso a paso, para alguien que no conoce el proyecto: [docs/guia-localhost.md](docs/guia-localhost.md) |

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
- La verificación local tiene que incluir `npm run lint`, porque el CI lo exige. En la Fase 13
  no se corrió y se escapó un BOM literal en una expresión regular de `e2e/support.ts`
  (`no-irregular-whitespace`); se corrigió escribiendo `\uFEFF`.

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
