# ADR 0034 — Libros de Excel y varios archivos por fuente

**Estado:** aceptada · **Fecha:** 2026-10-07 · **Relacionado:** [ADR 0010](0010-la-carga-de-archivos-como-superficie-de-ataque.md), [ADR 0027](0027-el-mapping-de-columnas-se-decide-antes-de-cargar.md), [ADR 0028](0028-la-lectura-tolera-los-exports-reales.md), [ADR 0030](0030-una-carga-grande-cabe-en-el-pod.md)

## Contexto

El 2026-10-07 llegaron los exports de APIM de la organización en tres archivos, uno por región
(`APIM_Onboarded_EAIs_EMEA.csv`, `_US.csv`, `_LATAM.csv`), con la misma cabecera: `EAI`,
`Country`, `Application Short Name` y `Application Name`. La carga los rechazó: aceptaba un
solo archivo por fuente y el segundo se informaba como *It duplicates a file already received
for the 'APIM Onboarding' source*. Algunos exports llegan además como libro de Excel, y la carga
solo aceptaba CSV.

Se pidió aceptar `.xlsx` y que varios archivos con las mismas cabeceras se carguen juntos,
complementándose.

## Decisión

1. **Varios archivos por fuente.** Los archivos que se reconocen como la misma fuente se leen
   como una sola, con las filas de todos en el orden en que se eligieron
   (`CsvSourceReader.ReadParts`). Cada archivo conserva su veredicto, sus números de fila y sus
   motivos de rechazo, y su mapping se resuelve por separado: dos regiones pueden nombrar distinto
   una columna.
2. **La clave única sigue siendo única.** En el maestro EAI, una aplicación que aparece en un
   archivo posterior queda con la fila de ese archivo, igual que dentro de un archivo queda la
   última, y la anterior se rechaza diciendo en qué archivo volvió (`row.duplicateKeyInFile`).
   Las fuentes con muchas filas por aplicación (SCA, monitoreo, APIM) simplemente se suman.
3. **El mismo archivo dos veces se rechaza.** Sumar dos copias duplicaría hallazgos de SCA o
   filas de APIM sin que nadie lo note, así que cada archivo se identifica por su SHA-256 y una
   copia exacta se rechaza nombrando el original (`file.identical`). Recalcular sin ella no
   cambia nada, y la pantalla lo dice.
4. **Cómo se guarda.** En el adaptador de archivos, el primer archivo de cada fuente conserva el
   nombre canónico y los siguientes quedan como `apim_onboarded_eais.part2.csv`,
   `apim_onboarded_eais.part3.csv`. Una carga de un archivo por fuente queda igual que antes. El
   manifiesto registra en `Parts` la fuente, el nombre guardado y el nombre con que llegó cada
   archivo; al releer solo se aceptan nombres de archivo planos. En la base de datos, las filas de
   todos los archivos entran en la misma tabla de la carga. El linaje dice de qué archivo es cada
   fila rechazada.
5. **Libros de Excel.** Un `.xlsx` se convierte a CSV apenas llega
   (`XlsxSourceConverter`), y desde ahí sigue el camino de siempre: identificación por
   encabezado, mapping, validación fila por fila y lo que queda guardado. Qué es un archivo lo
   deciden sus primeros bytes, no su extensión. Reglas de lectura:
   - Se lee la primera hoja visible y la primera fila con valores es el encabezado. Las filas
     vacías se saltan.
   - Las fórmulas valen su último resultado guardado. Las celdas con error quedan vacías.
   - Las fechas salen en ISO 8601 según el formato de la celda.
   - Los números salen en formato invariante, con 15 cifras y sin exponente. Un número como
     1.234 se escribe 1.2340, porque el lector lee 1.234 como miles en un campo entero.
   - Un libro protegido con contraseña, o un `.xls` antiguo, se rechaza con su motivo.
6. **Sin dependencias nuevas y con defensas explícitas.** El ZIP y el XML los lee el framework
   (.NET y, en el navegador, `DecompressionStream`):
   - el directorio del ZIP se mide antes de abrirlo (máximo 10 000 entradas);
   - cada parte tiene un tope de bytes descomprimidos que se cuenta al leer, sin creer lo que
     declara el ZIP;
   - el XML se lee sin DTD ni entidades externas;
   - los textos compartidos (32 M de caracteres), las columnas (1 000) y las filas tienen tope,
     y el CSV resultante tiene el mismo tope de 128 MB que un CSV subido;
   - nada se extrae con el nombre que trae el ZIP.
7. **El mapping se revisa igual.** El navegador lee del libro solo la fila de encabezado: el
   directorio, el libro, el principio de la primera hoja visible y los textos compartidos que
   esa fila usa. Ninguna fila de datos sale del equipo hasta cargar. Si el navegador no puede
   leer un libro, la carga sigue siendo posible: cada columna se reconoce por su nombre o alias.
8. **Topes.** Hasta 40 archivos por carga (antes 12); los 256 MB por carga no cambian.

## Alternativas consideradas

- **Una librería de Excel (ClosedXML, ExcelDataReader, SheetJS):** más superficie de cadena de
  suministro y, en la máquina de la empresa, una restauración de paquetes que puede no llegar.
  La versión de SheetJS publicada en npm tiene vulnerabilidades conocidas.
- **Unir los archivos en uno antes de guardar:** los números de fila del linaje dejarían de
  corresponder al archivo de quien carga.
- **Exigir una cabecera idéntica entre archivos de una fuente:** cada archivo ya se identifica y
  se mapea solo; exigirla rechazaría regiones que nombran distinto una columna.

## Consecuencias

- Los tres archivos de APIM se cargan juntos. En la prueba en vivo con libros guardados por
  Excel 16, una carga con el maestro en `.xlsx` y APIM en tres archivos (dos en Excel y uno en
  CSV) dio exactamente los mismos 250 scores y las mismas columnas que la carga toda en CSV.
- El encabezado de un libro tiene que estar en la primera fila con valores: un título encima lo
  ocuparía. La pantalla muestra lo que leyó, como con un CSV.
- Un `.xlsm` o un `.xls` siguen sin aceptarse; se guardan como `.xlsx` o CSV.
