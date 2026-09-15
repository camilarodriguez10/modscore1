# ADR 0003 — Ausencia de dato frente a valor cero

**Estado:** aceptada, modificada por [ADR 0019](0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md) · **Fecha:** 2026-09-10

## Contexto

La formula del BRD excluye del numerador y del denominador toda metrica sin dato.
Eso convierte la distincion entre "no hay dato" y "el valor es cero" en la decision
mas consecuente del ensamblador: confundirlas invierte el resultado.

El caso critico es SCA. El archivo `veracode_sca_open_flaws` contiene una fila por
vulnerabilidad. Una aplicacion sin flaws abiertos no genera filas — y una que nunca
se escaneo, tampoco. Desde el archivo solo, ambas son indistinguibles.

## Decision

**Una aplicacion presente en el archivo SCA se considera escaneada.** Las severidades
sin filas abiertas valen 0, que el ruleset mapea a "None" y puntua 100.

**Una aplicacion ausente del archivo no se considera escaneada.** Sus cuatro metricas
SCA quedan sin dato y salen del calculo.

Para que la distincion sea observable, una aplicacion escaneada y limpia aparece en
el archivo con sus hallazgos ya resueltos (`FND_STT = Closed`, con `RSLV_DT`). El
generador dummy lo reproduce.

La misma regla aplica al resto de las fuentes: fila ausente significa sin dato; fila
presente con valor cero significa cero. **Excepcion desde el ADR 0019:** en Kenna,
OSS Score, RAW Tech Score y Operational Score el BRD ("ALL Sections") excluye el 0
como un nulo, porque sus tablas no lo nombran como dato propio. SCA sigue igual: su
tabla puntua "None".

## Consecuencias

- Una aplicacion que nunca se escaneo no recibe los 18 puntos de peso de SCA
  Critical y High. Su denominador baja y su score refleja solo lo que si se midio.
  Lo contrario — tratar la ausencia como cero flaws — regalaria la mejor nota
  posible justamente a lo que nadie reviso.
- Hay un test dedicado a la distincion, en el motor y en el contrato de fuentes.
- Cuando llegue el archivo productivo conviene confirmar si existe un listado de
  aplicaciones escaneadas. Si existe, reemplaza esta heuristica por un hecho.
