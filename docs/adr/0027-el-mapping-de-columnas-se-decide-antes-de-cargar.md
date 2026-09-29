# ADR 0027 — El mapping de columnas se decide antes de cargar, con solo los encabezados, y queda registrado con la carga

**Estado:** aceptada · **Fecha:** 2026-09-28 · **Relacionado:** [ADR 0010](0010-la-carga-de-archivos-como-superficie-de-ataque.md), [ADR 0017](0017-el-backend-no-redacta-frases.md)

## Contexto

La primera carga con datos reales de la organización terminó con el EAI Master en **0 filas y
3.869 rechazadas**, y la carga descartada. La pantalla decía cuántas filas quedaron fuera,
no por qué, y los datos no se pueden compartir para diagnosticarlo.

Pidieron además un **módulo de mapping previo a la carga**: el linaje describe lo que ya
entró, y hacía falta decidir *antes* de cargar **qué archivo y qué columna cargan cada valor
en la base**.

Hasta aquí un campo se reconocía solo por su nombre o por los alias del contrato
(`contracts/sources/source-schemas.json`). Un export con otro nombre de columna dejaba el
campo vacío sin aviso; y un export con una columna que coincidía por alias pero traía otra
cosa —`Users` con texto donde se esperaba la cantidad de usuarios— se cargaba igual y
rechazaba cada fila.

## Decisión

### 1. Por qué se rechazaron filas, agrupado por problema

El lector del CSV ya conocía el motivo de cada fila rechazada (columna, tipo esperado,
valor); la respuesta de la carga solo llevaba el conteo. Ahora cada archivo trae sus
**motivos agrupados** por código, columna y tipo esperado: el aviso de la primera fila que lo
tuvo, cuántas filas lo tienen y los números de las primeras cinco, hasta veinte motivos por
archivo. Cuenta **todas** las columnas malas de cada fila, no solo la primera: quien corrige
el archivo necesita la lista completa de una vez. La pantalla agrega una pista cuando el
valor delata la causa (marcadores de «sin dato», decimales en enteros, punto de miles,
columnas corridas) y no adivina en los demás casos.

### 2. El mapping se revisa con solo los encabezados

**Review column mapping** lee en el navegador la primera línea de cada archivo y manda solo
eso. La capa Channel rechaza un encabezado que traiga algo después de un salto de línea, y
las dos capas acotan el tamaño (64 KB por línea, 300 columnas, 12 archivos). La capa System
identifica la fuente por el encabezado, como en la carga, y propone qué columna carga cada
campo: por nombre, por alias, **como en la carga vigente** si el archivo sigue trayendo esa
columna, o ninguna. Devuelve también el tipo esperado y qué métricas del ruleset vigente usan
el campo, a partir del `source.field` de cada métrica (`MetricFeeds`).

### 3. El mapping viaja con la carga, por archivo

La carga acepta, junto a la fuente asignada de cada archivo, un objeto JSON de campo a
columna (o a `null` si el archivo no lo trae). Un mapping exige la fuente: es de un contrato
concreto. La capa System valida la forma (tamaño, cantidad, caracteres de control) y el
publicador lo valida contra el contrato y el archivo: campo inexistente, columna que no está,
una columna para dos campos, campo obligatorio sin columna. Cualquiera rechaza el archivo con
su motivo.

### 4. Se aplica reescribiendo el encabezado

El lector no cambia. El mapping se aplica **reescribiendo el encabezado** antes de leer: cada
columna elegida pasa a llamarse como su campo, y toda otra columna que el lector reconocería
por nombre o alias se marca con el prefijo `(not loaded) ` para que no cargue nada. En el
adaptador de archivos el CSV se guarda ya reescrito, así que **releer una versión vieja no
depende del mapping de hoy**; en el de base de datos la reescritura ocurre al leer, sin tocar
disco ni cargar el archivo entero en memoria. Un encabezado que ocupa más de una línea no se
reescribe: se rechaza en vez de adivinar.

### 5. Queda registrado con la carga

Cada carga registra qué columna cargó cada campo y si la eligió una persona: en el manifiesto
del adaptador de archivos y en una columna nueva `ColumnMaps` de `snapshot_version` en el de
base de datos (migración `MapeoDeColumnas`). Se registra también sin mapping, con lo que
reconocieron el nombre y los alias. De ahí sale la sugerencia de la próxima revisión y la
pestaña **Data → Mapping**, que muestra por fuente la columna de la carga vigente, los nombres
aceptados y las métricas que usan cada campo, sin elegir archivos.

### 6. Permisos

Revisar el mapping es parte de publicar: el mismo rol y alcance completo que la carga, en las
dos capas. La pestaña Mapping describe cargas completas, como el linaje: alcance completo.

## Alternativas consideradas

- **Un almacén de mappings propio, versionado y publicado aparte** (como las reglas): da un
  mapping sin cargar, pero agrega un tercer ciclo de publicación con su auditoría, y el mapping
  quedaría separado de los datos que describe. Registrado con la carga, cada versión dice por
  sí misma de dónde salió cada valor.
- **Meter el mapping en las versiones de reglas:** hereda versiones y auditoría, pero obliga a
  simular y reautenticar para renombrar una columna, y mezcla la configuración de la ingesta
  con la del score.
- **Agregar alias al contrato desde el portal:** un alias vale para todos y para siempre, y es
  justo lo que produjo el caso `Users`: una coincidencia por nombre que carga la columna
  equivocada.
- **Subir el archivo entero para revisarlo:** mandaría los datos antes de decidir si se cargan.
  Con el encabezado alcanza.

## Consecuencias

- La primera carga de un export nuevo pide una revisión; las siguientes la proponen sola.
- Cada versión registra la columna de cada campo desde el 2026-09-28. Las anteriores no lo
  tienen y la pestaña lo dice.
- Una migración nueva en el adaptador de base de datos (`20260928190000_MapeoDeColumnas`),
  aditiva y nullable: las filas existentes quedan con `NULL`.
- Las pruebas de punta a punta corren con datos generados, donde la carga está apagada: la de
  la pantalla de mapping simula las respuestas de carga, y lo que valida el servidor lo cubren
  las pruebas de API (ver HANDOFF).
- Al leer números, con los dos separadores (`1.234,5`) el último es el decimal. Antes se leía
  1,2345 sin aviso.
