import io
import os

ROOT = r"C:\Users\kmiro\Downloads\mod score"


def load(name):
    raw = io.open(os.path.join(ROOT, name), encoding="utf-8", newline="").read()
    return raw.replace("\r\n", "\n"), "\r\n" in raw


def save(name, s, crlf):
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(os.path.join(ROOT, name), "w", encoding="utf-8", newline="").write(s)
    print("ok", name)


def apply(name, pairs):
    s, crlf = load(name)
    for old, new in pairs:
        if old in s:
            s = s.replace(old, new, 1)
        elif new not in s:
            raise AssertionError(f"NO MATCH in {name}: {old[:110]!r}")
    save(name, s, crlf)


# ------------------------------------------------------------------ PLAN.md
resultado = """### 8.2 Resultado de la Fase 09 — Adaptador de base de datos

**Criterio cumplido:** la misma suite de contrato que verificó Dummy y File corre
contra una base real, y el EAI de referencia sigue dando **79,67** leído desde ella.

| Evidencia | Resultado |
|---|---|
| Suite de contrato del tercer adaptador | 127 pruebas en el proyecto de contratos (eran 98) |
| Total | **466 pruebas en verde**, cero paquetes vulnerables con EF Core y SQLite dentro |
| Extremo a extremo por HTTP | Subir los ocho CSV por la pantalla publica una versión en la base, y el portafolio responde con ella |
| Golden desde la base | `20 + (44.75 / 60) x 80 = 79.67`, idéntico a archivos |

**Qué se construyó**, más allá de leer de una base:

- **Un puerto de publicación** (`ISnapshotPublisher`), gemelo del de lectura. La
  capa System ya no conoce ni al adaptador de archivos ni al de base: publicar es
  mover un directorio o abrir una transacción, y desde arriba no se distingue.
- **El esquema tiene la forma de las fuentes**, no la del dominio, y la carga se ata
  con una propiedad sombra ([ADR 0014](docs/adr/0014-la-base-guarda-la-forma-de-las-fuentes.md)).
- **Los rechazos se guardan** en su tabla: con archivos se recalculan al leer, pero
  aquí el archivo ya no existe, y sin ellos el linaje se apagaría en silencio.
- **Retención declarativa** (`Database:RetainVersions`): conserva las N cargas más
  recientes. Con archivos había que borrar directorios a mano.
- **Migraciones de EF Core**, aplicadas al arrancar en un solo nodo y por un job de
  despliegue cuando haya varias réplicas.

**Dos desviaciones del plan, ambas deliberadas:**

1. **Sin tabla `score_snapshot`.** El plan la contemplaba para abaratar la historia.
   La Fase 08 midió el costo real y el cálculo cacheado por versión dejó la consulta
   en 0,006 s: precalcular añadiría una copia que mantener coherente con el ruleset
   vigente a cambio de milisegundos.
2. **El motor concreto sigue siendo RFC-07.** El modelo es agnóstico y se verifica
   contra SQLite, que es una base real —archivo, transacciones, índices,
   migraciones—. Añadir SQL Server o PostgreSQL cuando se confirme el destino es un
   paquete y un juego de migraciones; arrancar contra un motor no verificado falla
   explícitamente en lugar de suponer.

**EF Core no llega a la capa Channel:** el adaptador vive en su propio proyecto y la
referencia desde la capa System es privada. El generador de datos dummy y el lector
de archivos tampoco lo cargan.

"""

apply("PLAN.md", [
    ("| **9 — Adaptador BD** | 1 sem | `DatabaseSourceReader`, EF Core 10, migraciones, feature flag | Contract tests verdes contra BD real |",
     "| **9 — Adaptador BD** ✅ | ENTREGADA | `DatabaseSnapshotProvider`, EF Core 10, migraciones, publicación transaccional, feature flag | **Cumplido:** contract tests verdes contra base real y el golden 79,67 leído desde ella |"),
    ("## 8bis. Ampliación solicitada — fases 10 a 12", resultado + "## 8bis. Ampliación solicitada — fases 10 a 12"),
    ("### 7bis.2 Fase de base de datos (pendiente, Fase 09)",
     "### 7bis.2 Fase de base de datos (entregada en la Fase 09)"),
    ("| Score precalculado | Tabla `score_snapshot(version_id, eai_code, score, numerator, denominator)` para no recalcular la serie en cada consulta |",
     "| Score precalculado | **Descartado.** La Fase 08 dejó la historia en 0,006 s cacheando por versión; una copia precalculada añadiría coherencia que mantener a cambio de milisegundos |"),
    ("""│  │  ├─ Modernization.SystemApi/       # host HTTP de la capa System
│  │  └─ Modernization.Sources/         # adaptadores de fuentes
│  │     └─ {Dummy,Files,Database}/""",
     """│  │  ├─ Modernization.SystemApi/       # host HTTP de la capa System
│  │  ├─ Modernization.Sources/         # ensamblador + adaptadores Dummy y File
│  │  │  └─ {Dummy,Files,Export}/
│  │  └─ Modernization.Sources.Database/ # adaptador EF Core, migraciones y publicación"""),
    ("7. **Destino de despliegue** — AKS (coherente con las menciones a AKS/ASE del BRD) o alternativa.",
     "7. **Destino de despliegue** — AKS (coherente con las menciones a AKS/ASE del BRD) o alternativa. Decide también el **motor de base de datos**: el adaptador de la Fase 09 es agnóstico y hoy se verifica contra SQLite; SQL Server o PostgreSQL entran como un paquete y un juego de migraciones."),
])

# ---------------------------------------------------------------- README.md
fence = "`" * 3
base_de_datos = """## Correr sobre una base de datos

""" + fence + """bash
DataSource__Provider=Database Database__Provider=Sqlite \\
  ConnectionStrings__Modernization="Data Source=./data/modernization.db" \\
  dotnet run --project src/system/Modernization.SystemApi
""" + fence + """

El esquema se aplica solo al arrancar. La pantalla **Cargas** funciona igual que con
archivos: cada carga es una transacción en vez de un directorio, y
`Database__RetainVersions` conserva las N más recientes.

El motor de despliegue es una decisión abierta (RFC-07). El modelo es agnóstico y se
verifica contra SQLite, que es una base real; añadir otro motor es un paquete y un
juego de migraciones:

""" + fence + """bash
dotnet ef migrations add <Nombre> --project src/system/Modernization.Sources.Database
""" + fence + """

## Regenerar los datos dummy"""

apply("README.md", [
    ("435 tests.", "466 tests."),
    ("## Regenerar los datos dummy", base_de_datos),
    ("| 09 Adaptador de base de datos | pendiente |",
     """| 09 Adaptador de base de datos | entregada |
| 10 Experiencia de consulta | pendiente |
| 11 Sitio en inglés | pendiente |
| 12 Administración de reglas | pendiente |"""),
])

# --------------------------------------------------------------- ASVS L2
apply("docs/security/asvs-l2.md", [
    ("| 5.3.4 | Consultas parametrizadas | N/A | Sin base de datos hasta la Fase 09 (EF Core parametrizado) |",
     "| 5.3.4 | Consultas parametrizadas | Cumple | EF Core con LINQ en todo el adaptador: no hay una sola cadena de SQL construida a mano |"),
    ("| V5 Validación y codificación | 9 | 0 | 0 | 2 |", "| V5 Validación y codificación | 10 | 0 | 0 | 1 |"),
    ("| **Total** | **100** | **15** | **3** | **12** |", "| **Total** | **101** | **15** | **3** | **11** |"),
])

# ------------------------------------------------------- modelo de amenazas
apply("docs/security/modelo-de-amenazas.md", [
    ("| **D** | Staging huérfano llena el disco | Limpieza al arrancar | `InterruptedStagingIsCleanedButRecentOnesAreKept` | — |",
     """| **D** | Staging huérfano llena el disco | Limpieza al arrancar | `InterruptedStagingIsCleanedButRecentOnesAreKept` | — |
| **T** | Inyección SQL con el adaptador de base de datos | EF Core con LINQ; ni una cadena de SQL construida a mano | `DatabaseProviderContractTests` (22) · [ADR 0014](../adr/0014-la-base-guarda-la-forma-de-las-fuentes.md) | — |
| **T** | Una carga a medias en la base | Una transacción por carga: si algo falla, no queda nada | `AnUploadWithoutTheMasterIsRejectedAndChangesNothing` | — |
| **I** | Cadena de conexión expuesta | Llega como archivo del gestor de secretos, nunca como variable de entorno | `Secrets:Directory` · `DeploymentGuards` | — |
| **S** | La base alcanzable desde otros pods | NetworkPolicy de salida solo al destino de la base | Plantilla en `network-policies.yaml` | Pendiente del motor concreto (RFC-07) |"""),
])
