# ADR 0015 — El archivo se reconoce por su encabezado, no por su nombre

**Estado:** aceptada · **Fecha:** 2026-09-12 · **Fase:** 10

## Contexto

Hasta la Fase 09 la carga emparejaba cada archivo con su contrato por el nombre:
`tech_debt.csv`, o cualquier nombre que contuviera la raíz `tech_debt`. En la
práctica eso obligaba a renombrar cada export antes de subirlo, y fallaba en las dos
direcciones:

- `Inventario EAI septiembre.csv` se rechazaba aunque fuera exactamente el maestro.
- `tech_debt_viejo_NO_USAR.csv` se aceptaba sin mirar su contenido.

El nombre es, además, la parte de la carga que el cliente controla por completo. El
[ADR 0010](0010-la-carga-de-archivos-como-superficie-de-ataque.md) ya prohibía usarlo
como ruta; seguir usándolo para decidir qué es cada archivo era confiar en él para
algo casi igual de importante.

## Decisión

**Cada archivo se identifica por la firma de su encabezado.** Cada contrato de
`source-schemas.json` ya declara sus columnas y sus alias, así que el archivo se
reconoce por lo que trae dentro.

El orden de resolución es deliberado:

1. **Asignación explícita** del usuario, validada contra la lista blanca del catálogo.
2. **Firma del encabezado.** Un contrato es viable si están todas sus columnas
   obligatorias y al menos **dos** de sus columnas; entre los viables gana el que
   reconoce más columnas y deja menos sin reconocer, y **solo si es estrictamente
   mejor**. Un empate no se decide por el orden del catálogo.
3. **El nombre, solo para desempatar** entre los que empataron, o cuando ningún
   contrato es viable (para que un archivo mal formado siga recibiendo el error
   accionable de "faltan columnas" en lugar de "no reconocido").

Cuando nada resuelve, **se devuelve lo que se leyó**: las columnas del encabezado,
acotadas y limpias, y las fuentes posibles. La interfaz lo muestra y deja asignar la
fuente a mano antes de reintentar.

**El archivo aterriza con un nombre generado** (`<guid>.part`) y solo se mueve a su
nombre canónico cuando el contrato queda identificado. El nombre del cliente no
participa ni en la decisión ni en la ruta.

Los dos publicadores —archivos y base de datos— usan el mismo `SourceFileMatcher`.

## Consecuencias

- **Ya no hay que renombrar exports.** Es el pedido del punto 6 y lo verifica una
  carga completa con ocho nombres cualquiera, que publica y sigue dando 79,67 en el
  caso de referencia.
- **El control se aprieta, no se relaja.** Lo que se escribe en disco sigue siendo el
  nombre canónico del contrato; lo nuevo es que decidir *qué* contrato ya no depende
  de una cadena del cliente.
- **La asignación manual es entrada del cliente** y se trata como tal: forma de
  nombre de archivo en las dos capas, pertenencia al catálogo en el emparejador, y una
  lista que no se alinea uno a uno con los archivos se rechaza entera.
- **Lo leído vuelve a una pantalla y a un log**, así que se acota a 40 columnas de 64
  caracteres y sin caracteres de control.
- **Hay ambigüedades reales.** Un encabezado `EAI,Country` encaja igual en el maestro
  (por sus alias) que en APIM. Es correcto preguntar: adivinar entre dos fuentes que
  alimentan métricas distintas movería scores sin que nadie lo note.
- **Leer el encabezado cuesta una lectura extra** en el adaptador de base de datos
  (el stream se abre dos veces). En el de archivos no: el archivo ya está en staging.
- **El umbral de dos columnas es una decisión, no un hallazgo.** Con uno, un archivo
  que solo trae la clave —que casi todas las fuentes comparten— quedaría asignado por
  descarte. Si aparece una fuente legítima de una sola columna, se asigna a mano.
