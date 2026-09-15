# Modernization Score Platform

Recreación del cálculo de **Modernization Score v1.3** sobre fuentes dummy, con
backend .NET 10 en dos capas API-led y SPA React 19.

El plan completo está en [PLAN.md](PLAN.md). Las decisiones de arquitectura, en
[docs/adr](docs/adr).

## Levantar el stack

Tres procesos, en este orden:

```bash
dotnet run --project src/system/Modernization.SystemApi    # http://localhost:5101
```

```bash
dotnet run --project src/channel/Modernization.ChannelApi  # http://localhost:5100
```

```bash
npm --prefix src/web run dev                               # http://localhost:5173
```

La SPA se abre en <http://localhost:5173>. El botón de login usa el esquema de
desarrollo, que **no autentica a nadie**: solo existe para trabajar sin un
proveedor OIDC levantado, y el proceso no arranca si se activa fuera del entorno
`Development`.

## Verificar

```bash
dotnet test ModernizationScore.slnx
```

598 tests. Los dos que importan más reproducen los casos documentados:
**EAI 14381 = 79,67** (ejemplo trabajado del BRD, derivado desde datos crudos por
el pipeline completo) y **EAI 10969 = 79,61** (dashboard Power BI v1.3, métrica
por métrica).

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
enlace: `/roadmap?country=Mexico&groupBy=cio`. Cada estadística es un filtro —un clic
en «Mexico» acota todas las pantallas— y el filtro sigue puesto al cambiar de menú.
Un filtro solo acota dentro del alcance del usuario: quien abre el enlace ve lo que
su alcance permite, nunca más.

En **Datos**, los exports se suben con cualquier nombre: cada archivo se reconoce por
su encabezado. Si un encabezado encaja en dos fuentes, la pantalla muestra lo que leyó
y deja asignar la fuente a mano.

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

## Prueba de carga

```bash
dotnet run --project tools/Modernization.LoadTest -- --duration 15 --concurrency 16
```

Mide la capa System directamente y la Channel con una sesión de desarrollo. La
Channel limita a 120 peticiones por minuto por usuario: para medirla hay que
levantarla con `RateLimiting__RequestsPerMinute` alto.

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
contracts/ruleset-v1.3.json     pesos, bandas y procedencia — reglas como datos
data/dummy/                     los ocho CSV generados
src/shared/                     dominio, motor de scoring, seguridad
src/system/                     capa System: reglas de negocio + fuentes
src/channel/                    capa Channel: BFF y composición por pantalla
src/web/                        SPA React 19
tests/                          golden tests, contract tests, E2E y seguridad
tools/                          generador de datos dummy y prueba de carga
docs/                           ADR, seguridad y runbooks
deploy/                         imágenes y manifiestos del cluster
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

Los supuestos abiertos están en la sección 10 de [PLAN.md](PLAN.md) como RFC-01 a
RFC-12. Todos tienen un default implementado y configurable; ninguno bloquea.
