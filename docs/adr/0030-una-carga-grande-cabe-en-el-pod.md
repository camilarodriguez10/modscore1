# ADR 0030 — Una carga grande cabe en el pod: filas compactas y 128 MB por archivo

**Estado:** aceptada · **Fecha:** 2026-10-05 · **Relacionado:** [ADR 0027](0027-el-mapping-de-columnas-se-decide-antes-de-cargar.md), [ADR 0028](0028-la-lectura-tolera-los-exports-reales.md)

## Contexto

El 2026-10-05 la pantalla de carga dejó fuera el export real de **Veracode SCA**: pesa
74.689 KB (73 MB) y el tope era de **64 MB por archivo** (256 MB por carga). El tope estaba en
dos lugares, `PublishLimits.MaxFileBytes` (System, los dos adaptadores) y
`ChannelLimits.UploadMaxFileBytes` (Channel, que además lo informa al sitio).

Antes de subirlo se midió qué pasa con un archivo así. Un SCA sintético de 70 MB (350 mil
filas, replicando las del dummy) se subió por la Channel, igual que desde el sitio, con la
System limitada como en el pod: 1 GiB de memoria, lo que deja un heap de 768 MiB
(`DOTNET_GCHeapHardLimit`), y un solo heap de GC. **Terminó en `OutOfMemoryException`.** Es
decir, el tope anterior de 64 MB ya no era seguro en el pod: nadie lo había medido.

La causa estaba en la lectura, no en el tope. `CsvSourceReader` guardaba cada fila como un
diccionario propio de 19 entradas, y una copia nueva de cada texto aunque se repitiera fila
tras fila (la aplicación, la severidad, la librería). Además, la carga lee el archivo dos veces:
una para validarlo y otra, al publicar, para armar la versión nueva.

## Decisión

1. **Filas compactas.** Cada fila guarda un arreglo de valores sobre un índice de columnas que
   comparte todo el archivo, en lugar de un diccionario por fila. `Row.Values` sigue siendo un
   `IReadOnlyDictionary<string, object?>` con todas las columnas del contrato, en orden y con
   null para las que el archivo no trae: los consumidores no cambian.
2. **Textos repetidos una sola vez:** `CacheFields` de CsvHelper, una caché de tamaño fijo. Un
   archivo de valores únicos no la hace crecer.
3. **128 MB por archivo**, en las dos capas. El total por carga sigue en 256 MB, y el tope de
   500 mil filas del lector sigue igual: es el que acota la memoria. Una prueba exige que la
   Channel y la System tengan los mismos topes.

Medido con el mismo límite de memoria del pod, después del cambio:

| SCA sintético | Antes | Después |
|---|---|---|
| 350 mil filas, 70 MB | `OutOfMemoryException` | Aceptado en 9,6 s; pico de 675 MB del proceso |
| 490 mil filas, 98 MB | — | Aceptado en 11,5 s; pico de 815 MB del proceso |

Lo que asigna cada fila al leerse bajó de 2.893 a 1.581 bytes (`ALargeExportAllocatesLittlePerRow`
falla por encima de 2.000), y lo que queda retenido bajó más, porque el diccionario era lo que
quedaba vivo.

## Alternativas consideradas

- **Solo subir el tope:** con la lectura anterior, el archivo real habría agotado la memoria
  del pod.
- **Subir también el tope de filas:** con 128 MB y filas cortas cabrían unas 640 mil, por
  encima de lo medido. Se deja en 500 mil hasta medir más.
- **Quitar columnas en el navegador antes de subir** (el mapping ya sabe cuáles se usan): mandaría
  menos datos, pero exige leer CSV en el navegador igual que en el servidor. Queda como opción si
  los exports siguen creciendo.
- **Leer una sola vez:** evitaría la segunda lectura al publicar, pero cambia la forma en que
  los dos adaptadores arman la versión. Queda registrado como TD-17.

## Consecuencias

- El export real de SCA entra. Uno de hasta 98 MB se midió dentro del pod. Por encima, puede
  frenarlo antes el tope de 500 mil filas que el de bytes, con un motivo claro (`csv.tooManyRows`).
- El margen en el pod es de unos 200 MB con 490 mil filas. Si la carga pesada se vuelve
  habitual, conviene TD-17 o más memoria para la System.
- Las pruebas que leen filas siguen igual; se agregaron tres: memoria por fila, columnas de la
  fila y paridad de topes.

## Ampliación del mismo día: el tope por campo

Con los alias del export real de Veracode, el archivo se rechazó por una celda de más de 4 096
caracteres en la fila 5624. El export trae texto libre que el cálculo no usa —lo más probable es
que la celda sea la descripción de una vulnerabilidad, `SUM_DSCR`— y un texto largo lo supera. El tope por campo pasa a
**65 536 caracteres**: sigue cortando una comilla sin cerrar, que se come las filas siguientes,
o un campo fabricado para agotar memoria, y lo que se retiene sigue acotado por los 128 MB del
archivo. El rechazo dice ahora la columna además de la fila, para encontrar la celda sin
compartir el archivo.
