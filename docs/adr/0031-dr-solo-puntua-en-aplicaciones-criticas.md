# ADR 0031 — DR solo puntúa en aplicaciones críticas

**Estado:** aceptada · **Fecha:** 2026-10-07 · **Relacionado:** [ADR 0002](0002-reglas-de-scoring-como-datos.md), [ADR 0003](0003-ausencia-de-dato-frente-a-valor-cero.md), [ADR 0019](0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md)

## Contexto

Al validar el sitio contra el reporte *Modernization Score v1.3* de Power BI, una aplicación
de LATAM daba 73,49 en el reporte y 65,89 en el sitio, y la diferencia estaba en el pilar de
riesgo. El 2026-10-07 el dueño del reporte compartió la regla con que calcula DR:

> "DR Inscope Value": if [Critical Application Indicator] = "N" then null, else if
> [DR_In_Scope] = "Yes" then 100, else if [DR_In_Scope] = "No" then 20, else null.

El sitio aplicaba solo la segunda parte: puntuaba `DR_In_Scope` en todas las aplicaciones. En
una aplicación no crítica sin DR, eso metía un 20 con peso 7 en el pilar de riesgo, donde el
reporte no pone nada.

En la misma conversación confirmó dos cosas más que no cambian el cálculo:

- **Architectural Value** es el promedio de los componentes ponderado por cantidad: Modern
  100, Heritage 60, Not Approved for Use 20, Obsolete 20. Con su ejemplo (408 Modern, 415
  Heritage, 105 Not Approved) da 73,06. Es lo que el sitio ya hacía.
- **Origen de dos métricas:** la deuda técnica sale de la vista `V_TECH_DEBT_APP_SCORE`, y en
  origen el Operational Score sale de los datos de visibilidad `MV_APP_MDRNZTN_DTL`.

## Decisión

1. **Una métrica puede declararse solo para críticas** en el ruleset: `criticalOnly: true`.
   Es dato, como `zeroIsNoData` (ADR 0002): se versiona, se audita y la nota de la métrica la
   muestra en la pantalla de reglas. El ruleset v1.3 la declara en `dr.inScope`.
2. **El motor la aplica antes de leer el valor.** Con el Critical Application Indicator en «N»,
   la métrica queda sin dato, con el motivo `exclusion.notCritical` («Only scored for critical
   applications, and this one is not critical»), y sale del numerador y del denominador.
   **Un indicador desconocido no excluye**, igual que en el reporte, que solo corta con «N».
3. **El laboratorio de escenarios no ofrece una palanca que no movería nada.** Si la métrica
   de una palanca es `criticalOnly` y la aplicación no es crítica, la palanca queda como no
   accionable, con el motivo `lever.notCritical`. Sale del ruleset activo, no de la palanca.
4. **El origen de la deuda técnica** (`V_TECH_DEBT_APP_SCORE`) queda en el ruleset y en el
   contrato de fuentes. **El Operational Score se sigue leyendo del archivo EAI/CMT, columna
   `Operational_Score`** (decisión del 2026-10-07), aunque en origen
   salga de `MV_APP_MDRNZTN_DTL`. El encabezado se reconoce sin importar mayúsculas
   (`operational_score`).

## Alternativas consideradas

- **Codificar la condición en el extractor de DR**, como la doble entrada de MFA: funciona,
  pero deja una regla de negocio fuera del ruleset, sin versión ni auditoría.
- **Excluir también cuando el indicador es desconocido:** sería más estricto que el reporte,
  y el objetivo es reproducirlo.

## Consecuencias

- Las aplicaciones no críticas con DR informado cambian de score: sale una métrica de peso 7
  del pilar de riesgo. Si tenían «No» (20), el score sube; si tenían «Yes» (100), baja.
- Los dos casos dorados no cambian: la 14381 no tiene DR y la 10969 no declara criticidad.
- En los datos de ejemplo, la 14381 no es crítica, así que «Include in the DR plan» ya no se
  le ofrece; las pruebas de escenarios usan otras palancas.
- Una versión de reglas publicada antes del 2026-10-07 no trae `criticalOnly`. Si está activa,
  DR se sigue puntuando en todas. Para que aplique la regla nueva, en **Rules → Versions** se
  restaura la v1.3 de fábrica (que ya la trae) y se publica, con los demás ajustes que hagan
  falta.
