# ADR 0028 — La lectura tolera los exports reales: un valor ilegible en un campo opcional queda sin dato

**Estado:** aceptada · **Fecha:** 2026-10-02 · **Relacionado:** [ADR 0019](0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md), [ADR 0027](0027-el-mapping-de-columnas-se-decide-antes-de-cargar.md)

## Contexto

La primera carga con datos reales dejó el EAI Master en 0 filas y 3.869 rechazadas. El
2026-10-02 la organización compartió el encabezado de su export, que sale de **Power BI**:

- Los campos resumidos llegan como **«Suma de Kenna_Score»**, «Suma de
  L1_Business_Capability_Count», «Suma de Operational_Score» y «Suma de Raw_Tech_Score».
- Trae **«MFA»** además de «Does this Application Utilize MFA?».
- Trae la jerarquía de fecha **Year, Quarter, Month, Day**: puede haber más de una fila por
  aplicación.
- **No trae APP_ID**, que es la única unión de la deuda técnica con una aplicación.

Y la regla de lectura era: una fila con cualquier valor que no se pudiera convertir se
rechazaba entera. En el EAI Master una fila es una aplicación: una sola columna numérica mal
formateada en todas las filas —un rango en User Count, un «N/A», un número con formato
regional— dejaba al portafolio sin ninguna aplicación.

## Decisión

1. **Un valor ilegible en un campo opcional queda sin dato y la fila entra.** Es lo que el
   BRD hace con un valor que falta: el campo sale del cálculo (ADR 0019). Se informa en el
   resultado de la carga como *Values loaded as empty*, agrupado igual que los rechazos
   —columna, tipo esperado, el valor de la primera fila, cuántas filas y las primeras—. Una
   fila sin clave o con un valor ilegible en un campo **obligatorio** se sigue rechazando.
2. **Los marcadores de «sin dato» son vacío:** `N/A`, `#N/A`, `-`, `NULL`, `(En blanco)`,
   `(Blank)`, `sin dato` y similares, en todo campo que no sea texto libre. Antes rechazaban la
   fila; no dicen que el archivo esté mal, dicen que esa aplicación no tiene el dato.
3. **Números en cualquier configuración regional.** Con los dos separadores, el último es el
   decimal; un separador repetido en grupos de tres es de miles; en un campo entero, un único
   separador seguido de tres dígitos es de miles («1.234» usuarios); en todo otro caso, el
   decimal. Un entero acepta decimales en cero («650,00», como exporta Power BI un «Suma de»)
   y rechaza los distintos de cero. Antes «650,00» se leía 65000 sin aviso.
4. **`VERDADERO`/`FALSO`** como sí/no, y más formatos de fecha (`d/M/yyyy`, `dd-MM-yyyy`,
   `yyyy/MM/dd`, ISO sin zona).
5. **Los identificadores se normalizan** (tipo `code` en el contrato): un EAI que un export
   trató como número —«14,381», «14.381», «14381.0», `="00123"`— vuelve a sus dígitos, para
   que coincida con los otros archivos y con las direcciones del sitio.
6. **Gana la mejor columna, no la primera.** El nombre exacto del campo antes que cualquier
   alias, los alias en su orden, y después los mismos nombres con un prefijo de resumen
   («Suma de», «Sum of», «Promedio de», «Average of», «Mín./Máx. de», «Primero/Último de»).
   «Recuento de» no, porque cuenta filas en lugar de traer el dato. Con eso «Does this
   Application Utilize MFA?» gana a «MFA» aunque venga después, y «Suma de
   L1_Business_Capability_Count» se reconoce sin agregarlo al contrato. La revisión del mapping
   propone exactamente lo mismo.
7. **El EAI no se repite.** El contrato marca la clave del maestro como única: queda la última
   fila de cada EAI y las anteriores se rechazan con su motivo (`row.duplicateKey`). Antes
   entraban todas —dos aplicaciones con el mismo EAI— y el adaptador de base de datos fallaba
   entero por su índice único.
8. **Sin APP_ID en el maestro, la deuda técnica se une por el EAI.** La organización confirmó
   que su APP_ID es el mismo código que el EAI. Si el maestro trae la columna, manda la columna
   (los datos de ejemplo tienen APP_ID distintos); si no la trae o viene vacía en una fila, se
   usa el EAI_Code, normalizado igual que el APP_ID del archivo de deuda técnica.

## Alternativas consideradas

- **Seguir rechazando la fila:** protege de cargar un dato malo, pero el costo es la aplicación
  entera, y en un export real eso fue el portafolio entero. Sin dato es lo que el modelo ya
  sabe tratar.
- **Interpretar rangos («100-500») o textos («Internal») como números:** sería inventar el dato.
  Quedan sin dato e informados; la pista dice cómo corregirlos.
- **Agregar cada variante como alias en el contrato:** «Suma de», «Sum of», «Promedio de»… por
  cada campo y cada idioma. La regla del prefijo lo cubre una vez para todos.
- **Unir la deuda técnica por el APP_ID que trae el archivo de SCA** junto a cada EAI: se
  implementó primero y se reemplazó el mismo día, cuando la organización confirmó que APP_ID y
  EAI son el mismo código. Solo cubría las aplicaciones escaneadas.
- **Exigir APP_ID en el maestro:** sobra si es el mismo código que el EAI.

## Consecuencias

- Un archivo con problemas de formato carga igual, y la pantalla dice qué quedó sin dato. El
  riesgo pasa de «no se carga nada» a «se carga con huecos visibles»: hay que mirar *Values
  loaded as empty* después de cada carga.
- Lo que quedó sin dato se ve en el resultado de la carga, no en el linaje de una versión vieja
  (TD-16).
- Si en algún momento el APP_ID deja de ser el EAI, el maestro tiene que volver a traer la
  columna APP_ID: con ella, manda la columna.
- Las pruebas que suponían el rechazo de la fila por un valor opcional se reescribieron, y una
  de punta a punta carga el EAI Master de ejemplo con el formato de Power BI y comprueba que la
  14381 sigue dando 79,67.
