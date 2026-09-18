# Modernization Score Platform

Recreación del cálculo de **Modernization Score v1.3** sobre fuentes dummy, con
backend .NET 10 en dos capas API-led y SPA React 19.

El plan completo está en [PLAN.md](PLAN.md). Las decisiones de arquitectura, en
[docs/adr](docs/adr).

## Levantar el stack

> ¿Primera vez, o hay que levantarlo en otro equipo? La guía paso a paso, sin dar nada por
> sabido y con los problemas frecuentes, está en
> [docs/guia-localhost.md](docs/guia-localhost.md).

Tres procesos, en este orden. Las dos variables de las capas **no son opcionales**: sin
`ASPNETCORE_ENVIRONMENT=Development` la capa arranca como producción y se detiene a propósito
(pide `AllowedHosts` y secretos reales), y sin `ASPNETCORE_URLS` escucha en el 5000.

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5101 dotnet run --project src/system/Modernization.SystemApi
```

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5100 dotnet run --project src/channel/Modernization.ChannelApi
```

```bash
npm --prefix src/web run dev
```

En `cmd`, las variables van con `set "ASPNETCORE_ENVIRONMENT=Development"` en la misma
ventana, antes del `dotnet run`. En un equipo que **no deja ejecutar `.exe` sin firmar**,
agrega `-p:UseAppHost=false` —así solo corre `dotnet.exe`— y deja que la capa Channel sirva
el sitio con `ASPNETCORE_WEBROOT`: es el camino C de la
[guía paso a paso](docs/guia-localhost.md).

Desde la app de Claude, `.claude/launch.json` tiene las tres piezas —`system`, `channel` y
`web`— con esas variables y `-p:UseAppHost=false`. **Arrancar la System primero y la Channel
cuando la System ya responda:** la Channel referencia el proyecto de la System, y si las dos
compilan a la vez se pisan el mismo `.dll` (`CS2012`, archivo en uso).

La SPA se abre en <http://localhost:5173>. El botón de login usa el esquema de
desarrollo, que **no autentica a nadie**: solo existe para trabajar sin un
proveedor OIDC levantado, y el proceso no arranca si se activa fuera del entorno
`Development`.

## Verificar

```bash
dotnet test ModernizationScore.slnx
```

737 pruebas .NET: 242 del motor, 178 de contratos y 317 de API. Las dos que importan más
reproducen los casos documentados:

- **EAI 14381 = 79,67:** ejemplo trabajado del BRD, derivado desde datos crudos por el
  pipeline completo.
- **EAI 10969 = 79,61:** dashboard Power BI v1.3, métrica por métrica.

`ViewModelContractTests` falla si un modelo de vista de la capa Channel y su tipo
TypeScript difieren.

```bash
npm --prefix src/web test          # 47 pruebas unitarias (Vitest)
npm --prefix src/web run test:e2e  # 30 escenarios: 14 de flujo y 16 de accesibilidad (axe)
```

Las de punta a punta usan el Microsoft Edge instalado (no descargan navegadores) y levantan
las tres piezas con datos dummy, o reutilizan las que ya estén corriendo.

```bash
dotnet list ModernizationScore.slnx package --vulnerable --include-transitive
npm --prefix src/web audit --audit-level=moderate
```

Ambos deben reportar cero. El gate no es decorativo: ya rompió el build una vez,
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

```bash
Authentication__Development__Roles__0=portfolio.read Authentication__Development__Scopes__0=lob:Individual dotnet run --project src/channel/Modernization.ChannelApi
```

## Consultar y compartir

El sitio está en inglés; la documentación del proyecto sigue en español. Las rutas de las
fases anteriores (`/aplicaciones/…`, `/datos`) redirigen a las nuevas (`/applications/…`,
`/data`), así que un enlace guardado no se rompe.

Los filtros viven en la URL, así que una vista filtrada se comparte copiando el
enlace: `/roadmap?country=Mexico&groupBy=cio`. Son los del dashboard del Modernization
Score: `region`, `country`, `cio`, `cre`, `capability` (capacidad primaria), `status`
(estado de la aplicación), `eai` (código EAI), `critical`, más `lob`, `disposition`,
`roadmap` y `band`. Cada estadística es un filtro —un clic
en «Mexico» acota todas las pantallas— y el filtro sigue puesto al cambiar de menú.
Un filtro solo acota dentro del alcance del usuario: quien abre el enlace ve lo que
su alcance permite, nunca más.

En **Datos**, los exports se suben con cualquier nombre: cada archivo se reconoce por
su encabezado. Si un encabezado encaja en dos fuentes, la pantalla muestra lo que leyó
y deja asignar la fuente a mano.

## El dashboard ejecutivo

La pestaña **Dashboard** (`/dashboard`, la primera del menú) es la vista para dirección,
*Portfolio Health & Risk Intelligence*. Responde una pregunta —dónde se concentra el
riesgo y la oportunidad de modernización, por qué, y qué decisión de arquitectura hace
falta— con cinco indicadores y un panel por cada una de las seis preguntas:

| Panel | Qué muestra |
|---|---|
| Indicadores | Health Score, aplicaciones en riesgo, candidatas a modernizar, exposición de *currency* (EOS/EOL redefinido) y huecos de resiliencia, con su tendencia por carga |
| 1 · Salud | Mapa de calor de capacidad primaria contra región, país, CIO o disposición objetivo; reparto por tramo; tendencia |
| 2 · Prioridad | Matriz criticidad × oportunidad (burbuja = riesgo) y el top 10 |
| 3 · Complejidad | Seis dimensiones en un treemap —las que aún no tienen fuente, punteadas— y cuatro oportunidades de reducción |
| 4 · Riesgo | El mismo mapa de calor midiendo riesgo, el top 5 y el índice de concentración |
| 5 · Obsolescencia | Qué conceptos explican los puntos perdidos y cuánto pesa la vigencia tecnológica |
| 6 · Resiliencia | Nivel por dimensión y por aplicación; las dimensiones sin fuente se dicen como tales |

Cada celda, burbuja, barra o indicador abre el mismo detalle lateral, con los mismos
campos en todos los paneles. El eje del mapa de calor viaja en la URL junto a los
filtros: `/dashboard?region=LATAM&axis=cio`.

**Las definiciones son provisionales.** Lo que la propuesta deja por definir —Currency
Score y su umbral, Risk Score, niveles de criticidad y oportunidad, cortes de resiliencia—
tiene un default en `contracts/dashboard-v1.json`, y la pantalla lo marca con un chip.
El porqué de cada default está en
[ADR 0024](docs/adr/0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md) y las
decisiones abiertas, en [PLAN.md §8.10](PLAN.md). El mismo análisis está en
`GET /api/dashboard`.

## Las seis preguntas del portafolio

La pantalla **Insights** (`/insights`) responde en una sola vista las seis preguntas de
gestión, sobre el mismo recorte que el filtro tiene puesto:

| Pregunta | Qué muestra |
|---|---|
| *How healthy is the portfolio?* | Promedio y mediana, reparto por tramo, cobertura de dato y cuántas críticas están bajo el umbral sano |
| *Which applications should be modernized first?* | Impacto contra esfuerzo: lo que sube el score al ejecutar su roadmap, dividido por las semanas de sus hitos y ponderado por criticidad, usuarios y capacidades |
| *Where should we reduce complexity?* | Misma capacidad repetida en un país, la misma capacidad resuelta distinto en varios países, aplicaciones marcadas `Eliminate` todavía activas, y una capacidad escrita en muchos lenguajes |
| *Where is technology risk concentrated?* | Por LOB, país, CIO o capacidad: flaws críticos y altos, gestión de vulnerabilidades débil, componentes obsoletos y críticas expuestas a internet |
| *What is driving obsolescence?* | Los puntos que pierde el portafolio en cada métrica, con el peso sin dato en columna aparte |
| *Where are the resilience gaps?* | Críticas fuera del alcance de DR o sin monitoreo, score operacional bajo y aplicaciones sin RTO ni RPO |

Cada número viene con su aritmética, y la evidencia de cada fila es una frase con sus
datos, no una etiqueta. El método está en
[ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md).

Dos controles propios viajan en la URL junto a los filtros, así que una vista se comparte
copiando el enlace:

| Parámetro | Valores | Para qué |
|---|---|---|
| `duplicates` | 2 a 5 (por defecto 3) | A partir de cuántas aplicaciones una coincidencia ya es una concentración |
| `riskBy` | `lob`, `country`, `cio`, `capability` | Por qué dimensión se agrupa el riesgo |

Ejemplo: `/insights?country=Mexico&duplicates=2&riskBy=capability`.

El mismo análisis está en `GET /api/insights`, que acepta además `top` (1 a 50) para el
largo de cada lista. Con un alcance limitado, todo se calcula solo sobre lo que ese
alcance deja ver.

**Lo que hoy es indicativo:** el esfuerzo sale de las semanas del roadmap, que son por
roadmap y no por aplicación, y el valor de negocio es un proxy de criticidad, usuarios y
capacidades. Los datos que lo harían real están en
[docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md) como DQ-01 a DQ-07.

## Administrar las reglas

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

## Comparar reglas y descargar

- **Compare versions** (en Rules) abre `/admin/rules/compare`. Muestra qué cambia entre dos
  versiones, cuánto se mueve el portafolio y, con un código EAI, qué le pasa a esa aplicación
  métrica por métrica. Con **Your draft** como destino, simula el borrador de la pestaña
  antes de publicarlo.
- **Download CSV** en Portfolio y Roadmap descarga lo que se está viendo, con los mismos
  filtros y agrupación. En la comparación descarga cada aplicación antes y después.
- La **tendencia** en Data marca con líneas punteadas cuándo se publicaron reglas. Todos los
  puntos se calculan con las vigentes.
- Cuando se publican reglas nuevas, quien consulta ve un aviso durante 14 días.

| Variable | Capa | Por defecto | Para qué |
|---|---|---|---|
| `RateLimiting__ExportsPerMinute` | Channel | 10 | Descargas por usuario |
| `Portfolio__WarmupSeconds` | System | 15 | Cada cuánto se dejan calculados scores e historia; 0 lo apaga |
| `Uploads__RetainVersions` | System | 0 | Cuántas cargas conserva el adaptador de archivos; 0 conserva todas |

## Autenticación y credencial entre capas

Hoy: OIDC para las personas y un secreto compartido entre las dos capas. Con APIM
([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)), las dos cosas
cambian por configuración, sin desplegar otra versión. El paso a paso, en el
[runbook](docs/runbooks/paso-a-apim.md).

| Variable | Capa | Por defecto | Para qué |
|---|---|---|---|
| `ServiceToken__Mode` | System | `Static` | Qué acepta: el secreto compartido, `Jwt` de APIM, o `Both` durante el cambio |
| `ServiceToken__Jwt__Authority` · `__Issuer` · `__Audience` | System | — | De dónde salen las llaves y qué emisor y audiencia se exigen |
| `ServiceToken__Jwt__AllowedCallers__0` | System | vacío | Clientes aceptados (`azp`); vacío acepta cualquiera que el emisor autorice |
| `SystemApi__Credential__Mode` | Channel | `Static` | `ClientCredentials` pide un token de vida corta y lo renueva |
| `SystemApi__Credential__TokenEndpoint` · `__ClientId` · `__Scope` | Channel | — | Emisor, cliente y audiencia. El secreto va como archivo `SystemApi__Credential__ClientSecret` |
| `Authentication__Provider` | Channel | `Oidc` | `Apim` usa el flujo de reautenticación de APIM |
| `Authentication__Apim__AuthorizeParameters__*` | Channel | vacío | Parámetros extra de la autorización, por ejemplo `acr_values=mfa` |
| `Authorization__ScopeFromGroups__ClaimType` · `__Map__*` | Channel | vacío | Traduce grupos del IdP a alcance cuando no hay claim propio; el claim manda sobre los grupos |

Los CSV neutralizan textos que una planilla ejecutaría como fórmula, y cada descarga queda
auditada con el evento 4213.

## Paquete portable para probar en otro equipo

```powershell
powershell -ExecutionPolicy Bypass -File tools/portable/build-portable.ps1
```

Genera en `artifacts/` un `.zip` con las dos capas como ejecutables de un solo archivo y la
SPA dentro, más su SHA-256. El último `.zip` se versiona en el repositorio. En el otro equipo
se descomprime y se ejecuta `start.cmd`. Ver [tools/portable/README.md](tools/portable/README.md)
y, para el paso a paso completo, [docs/guia-localhost.md](docs/guia-localhost.md).

**No es un despliegue:** usa la autenticación de desarrollo, y por eso escucha solo en
`127.0.0.1`.

## Respaldo en git

```powershell
powershell -ExecutionPolicy Bypass -File tools/git/backup.ps1 -Message "Descripción del cambio" -Verify
```

Corre las pruebas y los escaneos; se niega a subir capturas del BRD, imágenes, llaves, `.env`
o archivos de más de 100 MB, y nunca fuerza el push. El repositorio es privado y guarda todo
lo creado salvo capturas e imágenes. Ver [docs/runbooks/respaldo-en-git.md](docs/runbooks/respaldo-en-git.md).

## Prueba de carga

```bash
dotnet run --project tools/Modernization.LoadTest -- --duration 15 --concurrency 16
```

Mide la capa System directamente y la Channel con una sesión de desarrollo: pantallas,
las seis preguntas, las descargas y la comparación de reglas. La Channel limita por usuario,
así que para medirla hay que levantarla con los límites **muy** altos —con 100 000 por
minuto la ventana se agota en el primer escenario y el resto mide 429—:

```bash
RateLimiting__RequestsPerMinute=100000000 RateLimiting__ExportsPerMinute=10000000 RateLimiting__RulesSimulationsPerMinute=10000000 dotnet run -c Release --project src/channel/Modernization.ChannelApi
```

La columna 429 del resultado tiene que quedar en cero.

`--scenario <texto>` corre solo los escenarios cuyo nombre lo contiene. Para medir como en
un pod pequeño —la capa System fijada a dos núcleos, simulaciones solas y lecturas mientras
simulan—:

```powershell
powershell -ExecutionPolicy Bypass -File tools/Modernization.LoadTest/medir-en-dos-nucleos.ps1
```

Con la capa System y la herramienta compiladas antes en Release.

## Correr sobre archivos en vez de datos generados

```bash
DataSource__Provider=File DataSource__Directory="$PWD/data/history" dotnet run --project src/system/Modernization.SystemApi
```

Las rutas van absolutas: `dotnet run --project` arranca con la carpeta del proyecto
como raíz, y una ruta relativa como `./data/history` apunta a un directorio que no
existe —el portafolio aparece sin cargas, sin ningún error—.

Con el adaptador de archivos, la pantalla **Cargas** permite subir los CSV y
recalcular. Cada carga se publica como una versión consultable, y la evolución del
score aparece en esa misma pantalla.

En el primer arranque no hay ninguna carga: las pantallas lo dicen y llevan a
**Cargas**, que es la única que funciona sin datos. Para arrancar necesitas al menos una carga. La más simple: genera los dummy,
cópialos a `data/history/<id>/` y escribe su `manifest.json` — o levanta el sistema
con el adaptador Dummy, exporta, y sube los archivos por la interfaz.

## Correr sobre una base de datos

```bash
DataSource__Provider=Database Database__Provider=Sqlite \
  ConnectionStrings__Modernization="Data Source=$PWD/data/modernization.db" \
  dotnet run --project src/system/Modernization.SystemApi
```

El esquema se aplica solo al arrancar. La pantalla **Cargas** funciona igual que con
archivos: cada carga es una transacción en vez de un directorio, y
`Database__RetainVersions` conserva las N más recientes.

El motor de despliegue es una decisión abierta (RFC-07). El modelo es agnóstico y se
verifica contra SQLite, que es una base real; añadir otro motor es un paquete y un
juego de migraciones:

```bash
dotnet ef migrations add <Nombre> --project src/system/Modernization.Sources.Database
```

## Regenerar los datos dummy

```bash
dotnet run --project tools/Modernization.DummyData -- data/dummy
```

Genera 250 aplicaciones deterministas y las exporta como ocho CSV con los nombres
de columna reales de cada archivo observado. Devuelve código de error si el EAI de
referencia deja de dar 79,67.

## Estructura

```
contracts/ruleset-v1.3.json     18 métricas: pesos, bandas y procedencia — reglas como datos
contracts/dashboard-v1.json     definiciones provisionales del dashboard ejecutivo
data/dummy/                     los ocho CSV generados
src/shared/                     dominio, motor de scoring, what-if, roadmap, insights y dashboard, reglas, seguridad
src/system/                     capa System: reglas de negocio + fuentes
src/channel/                    capa Channel: BFF, composición por pantalla y exportaciones
src/web/                        SPA React 19 · pruebas unitarias en src/ · e2e/ con Playwright
tests/                          golden tests, contract tests, E2E por HTTP y seguridad
tools/                          datos dummy, prueba de carga, respaldo en git y paquete portable
docs/                           ADR, seguridad, runbooks, índice del BRD y PENDING-ITEMS.md
deploy/                         imágenes y manifiestos del cluster
HANDOFF.md                      traspaso completo del contexto
```

## Estado

| Fase | Estado |
|---|---|
| 00 Fundaciones | entregada |
| 01 Motor de scoring | entregada |
| 02 Datos dummy | entregada |
| 03 Capas Channel y System | entregada |
| 04 SPA base | entregada |
| 05 What-If | entregada |
| 06 Roadmap | entregada |
| 07 Adaptador de archivos | entregada |
| 08 Endurecimiento | entregada |
| 09 Adaptador de base de datos | entregada |
| 10 Experiencia de consulta | entregada |
| 11 Sitio en inglés | entregada |
| 12 Administración de reglas | entregada |
| 13 Revisión y remediación | entregada |
| 14 Verificación de seguridad (ASVS L2) | en curso: hecho lo que no necesita un entorno real (PLAN §8.7) |
| 15 Autenticación con APIM | en curso: código listo y probado con dobles; falta el emisor real (PLAN §8.8) |
| Pantalla 09 — las seis preguntas | entregada (PLAN §8.9) |
| Dashboard ejecutivo | D1 y D2 entregadas; D3 (diccionario y edición desde el portal) y D4 (fuentes nuevas) planificadas (PLAN §8.10) |
| 16 Escala y operación | planificada |
| 17 Hallazgos de infraestructura (H-16, H-17, H-20, H-21) | planificada, al final por decisión |

Los supuestos abiertos están en la sección 10 de [PLAN.md](PLAN.md) como RFC-01 a
RFC-17; todos tienen un default implementado y configurable. Los pendientes para revisar
con el equipo, con contexto, opciones y criterio de aceptación, están en
[docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md). Para retomar el proyecto, empezar por
[HANDOFF.md](HANDOFF.md).
