# ADR 0014 — La base de datos guarda la forma de las fuentes

**Estado:** aceptada · **Fecha:** 2026-09-12 · **Motor concreto:** pendiente de RFC-07

## Contexto

La Fase 09 cierra la evolución prevista desde el principio: Dummy → archivos →
base de datos. La pregunta de diseño no es cómo hablar con una base, sino **qué se
guarda en ella**.

La tentación es guardar el modelo canónico —una tabla de aplicaciones con sus
métricas ya normalizadas— porque es lo que el motor consume. Sería un error.

## Decisión

**Una tabla por fuente, con las columnas de la fuente**, más la carga a la que
pertenece cada fila. `eai_master`, `sca_flaw`, `tech_debt`… con `EAI_CODE`,
`APP_ID`, `"1-Critical"` y `TRUE`/`FALSE` tal como llegan.

- **La traducción sigue ocurriendo una sola vez**, en el ensamblador, que es el
  mismo para los tres adaptadores. Si la base guardara el modelo canónico habría
  dos traducciones que mantener de acuerdo, y el mismo archivo podría puntuar
  distinto según por dónde entrara. Eso es exactamente lo que el contrato de
  adaptadores existe para impedir.
- **La carga se ata con una propiedad sombra** (`VersionId`), así que los registros
  de fuente son los mismos tipos que ya usan Dummy y File: ninguno lleva dentro un
  campo que solo existe por la persistencia.
- **Los rechazos se guardan** (`source_rejection`). Con archivos, un rechazo se
  vuelve a calcular en cada lectura; aquí el archivo original ya no existe, y sin
  esta tabla migrar a base apagaría en silencio media pantalla de linaje.

**Publicar es una transacción.** Es el equivalente exacto del movimiento atómico
del directorio: si algo falla a mitad, no queda una carga a medias, no queda nada.
Se agrega un puerto `ISnapshotPublisher` —gemelo del de lectura— que implementan el
adaptador de archivos y el de base de datos; la capa System ya no conoce a ninguno
de los dos.

**Retención declarativa.** `Database:RetainVersions` conserva las N cargas más
recientes y borra el resto dentro de la misma transacción. Con archivos había que
borrar directorios a mano (runbook de carga defectuosa).

**El motor concreto sigue abierto (RFC-07).** El modelo es agnóstico y se verifica
contra **SQLite**, que es una base real: archivo, transacciones, índices,
migraciones. Añadir SQL Server o PostgreSQL cuando se confirme el destino es un
paquete y un juego de migraciones, no un rediseño. Arrancar contra un motor no
verificado falla explícitamente, en lugar de suponer que funcionará.

## Consecuencias

- **EF Core no viaja al channel-api.** El adaptador vive en su propio proyecto y la
  referencia desde la capa System es privada: la capa Channel referencia a la
  System solo por sus contratos, y no tiene por qué cargar un ORM.
- **El CLI de datos dummy y el lector de archivos siguen sin EF Core**, que es lo
  que mantiene barato el árbol de dependencias que se audita en cada build.
- **Sin tabla de scores precalculados.** El plan (secc. 7bis.2) la contemplaba para
  la historia. La Fase 08 midió el costo real: con el cálculo cacheado por versión,
  la historia del portafolio pasó de 1,0 s a 0,006 s. Precalcular agregaría una
  copia que mantener coherente con el ruleset vigente a cambio de milisegundos; se
  deja fuera hasta que una medición la pida.
- **Las migraciones son código generado.** Se tratan como tal en el análisis: una
  migración editada a mano deja de coincidir con su snapshot.
- **El contrato de adaptadores se cobra su tercera deuda:** la misma suite que
  verificó Dummy y File corre contra la base sin cambiar una línea, y el EAI de
  referencia sigue dando 79,67.
