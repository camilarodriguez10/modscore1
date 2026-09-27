# ADR 0026 — El análisis de código llega de GitHub Enterprise por un recolector aparte, y el fin de vida sale de endoflife.date

**Estado:** aceptada · **Fecha:** 2026-09-26 · **Relacionado:** [ADR 0012](0012-el-alcance-se-aplica-sobre-los-datos.md), [ADR 0025](0025-las-definiciones-viajan-con-las-reglas.md)

## Contexto

El negocio pidió analizar el código de las aplicaciones directamente desde **GitHub
Enterprise**, sin configurar la integración todavía: dejarla en el plan y, con lo que se
puede obtener de cada repositorio, construir **una vista nueva de obsolescencia y riesgo
según las librerías, el software y los componentes que usa cada aplicación**, con **una
fuente para determinar el fin de vida**.

Hasta hoy la obsolescencia se mide con lo que *declara* el inventario: el Currency Score de
CMT (`Raw_Tech_Score`) y los conteos de componentes obsoletos, sin fechas (DQ-05). Lo que
el inventario no dice es qué corre de verdad cada aplicación, ni qué vence el trimestre que
viene.

Lo que se puede obtener de un repositorio de GitHub Enterprise **sin leer ni guardar su
código fuente**:

| Dato | De dónde |
|---|---|
| Lenguajes, rama principal, último commit, archivado | API de metadatos del repositorio |
| Runtimes, frameworks, imagen de sistema operativo, base de datos | Manifiestos conocidos en la rama principal: `global.json`, `*.csproj`, `.nvmrc`, `package.json`, `pom.xml`, `build.gradle`, `requirements.txt`, `.python-version`, `go.mod`, `Dockerfile`, `docker-compose.yml` |
| Bibliotecas, directas y transitivas | Grafo de dependencias (exportación SBOM en SPDX) |
| Vulnerabilidades abiertas por biblioteca | Alertas de Dependabot, alimentadas por la GitHub Advisory Database |
| A qué aplicación pertenece | Una propiedad personalizada del repositorio con el código EAI |

## Decisión

### 1. Una pantalla Code, que ya existe con datos de demostración

`/code`, en la navegación principal, con los mismos filtros y el mismo alcance que el resto
(ADR 0012). Responde cuatro cosas: **qué componentes llegaron o están por llegar a su fin
de vida**, **qué bibliotecas necesitan atención**, **dónde el código contradice al
inventario** y **qué aplicaciones cargan más riesgo de código**. Cada aplicación abre su
detalle: repositorios, componentes con su fecha y estado, bibliotecas con hallazgos y de
dónde sale cada punto de su índice.

Mientras no exista la integración, los repositorios son **de demostración**: se generan del
portafolio vigente, de forma determinista por código EAI; el lenguaje del maestro elige la
familia tecnológica y el Currency Score inclina las versiones. La pantalla lo dice en ámbar
antes que cualquier número. Ese proveedor (`CodeInventory:Provider = Sample`) **solo
arranca en Development**: fuera de ahí el proceso no inicia, para que un dato inventado
nunca se lea como real. El valor por defecto fuera de Development es `None`, y la pantalla
dice que no hay inventario.

### 2. La fuente del fin de vida es endoflife.date

Una foto de su API, versionada en `contracts/eol-catalog-v1.json`: 22 productos y 273
ciclos (runtimes, frameworks, servidores de aplicaciones, sistemas operativos y bases de
datos), consultada el 2026-09-26. El proyecto es abierto, con licencia MIT, y cada producto
enlaza a su fuente primaria.

- **Fin de vida** es el fin del soporte de seguridad gratuito. El soporte extendido pagado
  (Oracle, Spring, Microsoft) se muestra junto a la fecha pero **no cuenta como soporte**:
  depende de un contrato que el catálogo no conoce (GH-06).
- **Estados**, del peor al mejor: *past end of life* (la fecha pasó, o el ciclo está
  discontinuado sin fecha), *ending soon* (vence dentro de la ventana configurable),
  *support ended* (terminó el soporte activo y siguen los parches de seguridad), *supported*
  y *not assessable* (el producto o la versión no están en el catálogo).
- **Una versión cae en el ciclo más largo que la empieza**: `3.10.2` es Python 3.10 y no
  3.1; `2.12.4` de Log4j es la rama 2.12; `1.8.0_392` es Java 8.
- El estado se evalúa **contra el día de hoy**, no contra la fecha de la carga: un
  componente vence aunque nadie suba nada.

### 3. Las reglas son paramétricas, en un área nueva de las definiciones

El área `code` se suma a las siete de ADR 0025, con los mismos borrador, simulación,
reautenticación, auditoría y versiones: 14 parámetros —ventana de *ending soon*, mayores
de atraso para una biblioteca desactualizada, meses sin commits para un repositorio
abandonado, los puntos de cada hallazgo, los tres niveles y el largo de la lista— y la
regla `code.levels` (moderado < alto < crítico).

**Índice de riesgo de código** = la suma, sobre los repositorios de la aplicación, de los
puntos de cada componente vencido o por vencer, cada alerta crítica o alta, cada biblioteca
obsoleta o desactualizada y cada repositorio abandonado. Una biblioteca obsoleta cuenta una
sola vez. **Una aplicación sin repositorios no tiene nivel**: no es riesgo bajo, es que no
se ve.

**El índice no entra al Modernization Score.** El score lo define el BRD. Lo que sí se
muestra es la discrepancia con el Currency Score: *hidden obsolescence* (CMT la da por
vigente y el código declara componentes vencidos) e *inventory behind the code* (CMT la da
por legada y el código no declara nada vencido).

### 4. La integración: un recolector aparte, que no se expone

Cuando se haga, será **un servicio propio, `code-collector`, que corre como tarea
programada** y no atiende tráfico. Ni la capa System ni la Channel tienen credenciales de
GitHub:

```
GitHub Enterprise ──(GitHub App, solo lectura)──► code-collector (CronJob, sin ingress)
endoflife.date / espejo ───────────────────────►       │
registros de paquetes / espejo ────────────────►       │  POST interno: inventario validado
                                                       ▼
                                   capa System (dueña del almacenamiento, calcula)
                                                       │
                                   capa Channel (entrega) ──► SPA /code
```

- **Identidad:** una **GitHub App** instalada solo en las organizaciones elegidas, con
  permisos de **solo lectura**: *Metadata*, *Contents* (para leer la lista cerrada de
  manifiestos), *Dependabot alerts* y *Custom properties*. La clave privada vive en el
  gestor de secretos y llega como archivo, igual que el token de servicio; los tokens de
  instalación duran una hora.
- **Qué lee:** metadatos, el SBOM del grafo de dependencias, las alertas abiertas y los
  manifiestos de la lista, en la rama principal. **No clona ni guarda código**, no ejecuta
  nada de los repositorios —ni `npm install` ni Maven ni Gradle— y tiene tope de tamaño por
  archivo y por repositorio.
- **Mapeo:** repositorio → aplicación por la propiedad personalizada `eai-code` (GH-02).
  Lo que no tiene mapeo se informa aparte, solo a quien ve todo el portafolio.
- **Publicación:** el inventario llega a la capa System por un endpoint interno con el
  token de servicio y un rol propio, con validación de esquema y límites, y se versiona
  como una carga: auditoría, retención y vuelta atrás. La System sigue siendo la única
  dueña del almacenamiento y del cálculo; la Channel no cambia.
- **Red:** salida permitida solo hacia GitHub Enterprise, endoflife.date y los registros de
  paquetes, o sus espejos internos (GH-03).
- **Frecuencia:** diaria. El catálogo de fin de vida se refresca en la misma corrida y se
  valida antes de usarlo; si no valida, se conserva el anterior.

## Alternativas consideradas

- **Clonar los repositorios y escanearlos con Syft o Trivy dentro del cluster.** Ve más
  —dependencias vendorizadas, imágenes construidas— pero mueve código fuente al cluster,
  pide permisos de lectura completos y cómputo por repositorio. Queda como opción para las
  imágenes de contenedor, que ya se escanean en CI (GH-05).
- **Solo GitHub Advanced Security (Dependabot y CodeQL).** Cubre vulnerabilidades, no el fin
  de vida de runtimes, frameworks ni sistemas operativos.
- **OSV.dev en lugar de Dependabot** para las vulnerabilidades: abierto y sin licencia de
  GitHub Advanced Security. Se decide en GH-04.
- **Fechas de cada fabricante** (Microsoft Lifecycle, Oracle, Canonical): más autoritativas,
  pero una fuente y un formato por fabricante. endoflife.date las agrega y enlaza a cada una.
- **Calcular en la capa Channel:** no; la Channel compone y entrega, no calcula (ADR 0001).
- **Sumar el índice al Modernization Score:** no; el score lo define el BRD.

## Consecuencias

- La obsolescencia gana un eje de tiempo —qué vence y cuándo— para lo que declaran los
  repositorios, que es la mitad de DQ-05.
- El código se contrasta con lo que declara CMT, y las diferencias se nombran.
- El recolector aislado acota el daño si se compromete: tiene solo lectura en GitHub, no
  sirve tráfico y no guarda código.
- El catálogo envejece —los fabricantes extienden fechas—: hay que refrescarlo, y hasta la
  integración eso es regenerar el archivo y desplegar.
- Lo que no vive en GitHub —COBOL del mainframe, ABAP de SAP— queda fuera, y la pantalla lo
  muestra como aplicaciones sin repositorios (GH-08).
- Leer manifiestos dice la intención; la imagen que corre puede ser otra (GH-05).

## Verificación

- Motor: el catálogo carga, cada versión cae en su ciclo, los estados cambian con la fecha
  y la ventana, y el índice suma exactamente sus hallazgos (`EndOfLifeCatalogTests`,
  `CodeAnalyzerTests`).
- Fuentes: los repositorios de demostración son deterministas, cada uno pertenece a una
  aplicación y todo producto que generan está en el catálogo salvo el hueco deliberado de
  WebSphere (`SampleCodeInventoryProviderTests`).
- API: la pantalla atraviesa las dos capas con filtros y alcance, el detalle fuera de
  alcance es 404, el proveedor de demostración no arranca fuera de Development y sin
  inventario la pantalla lo dice (`CodeEndToEndTests`); los tipos de la SPA coinciden con
  el contrato (`ViewModelContractTests`).
- SPA: Vitest de textos y explicaciones; cinco escenarios e2e y axe en la pantalla y en el
  detalle, en claro y en oscuro.
