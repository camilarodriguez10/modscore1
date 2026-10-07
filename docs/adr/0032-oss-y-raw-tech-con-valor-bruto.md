# ADR 0032 — OSS y Raw Tech entran con su valor bruto, como el reporte v1.3

**Estado:** aceptada · **Fecha:** 2026-10-07 · **Relacionado:** [ADR 0002](0002-reglas-de-scoring-como-datos.md), [ADR 0019](0019-el-cero-se-excluye-salvo-que-la-tabla-lo-puntue.md), [ADR 0031](0031-dr-solo-puntua-en-aplicaciones-criticas.md)

## Contexto

El ruleset v1.3 mapeaba el Open-Source Score y el Raw Tech Score por bandas, como el BRD
escrito: 0-59 → 20, 60-74 → 60, 75-100 → 100. Al comparar una aplicación con la captura del
reporte *Modernization Score v1.3* del 2026-10-07, las dos métricas no coincidían:

| Métrica | Reporte | Sitio, con bandas |
|---|---|---|
| Open-Source Score | 37 × 10 % = **3,70** | 20 × 10 % = 2,00 |
| Raw Tech Score | 88,48 × 5 % = **4,42** | 100 × 5 % = 5,00 |

El reporte usa el valor tal como llega. Con eso, sus pilares cierran exactamente: riesgo
17,30 / 50 → 47,68, arquitectura 30,351 / 40 → 80,70 y score 66,12. La normalización del
pilar y del total (20 + 80 × numerador ÷ peso) es la misma en los dos. Quien define la regla
de negocio lo confirmó: no hace falta normalizar esas dos métricas, se calculan con el valor
bruto sobre su peso.

Los dos casos dorados se habían validado con las bandas: la 14381 (ejemplo trabajado del BRD,
79,67) y la 10969 (una captura anterior del reporte, 79,61).

## Decisión

1. **OSS y Raw Tech usan `identity`** en el ruleset de fábrica: el puntaje es el valor bruto,
   acotado a 0-100, por su peso. Kenna, Tech Debt y Operational siguen por bandas, como en el
   reporte.
2. **El cero se sigue excluyendo** (`zeroIsNoData`, ADR 0019). El ruleset ahora lo admite
   también en `identity`, no solo en bandas.
3. **Caso dorado nuevo:** la captura del reporte del 2026-10-07 tiene que dar 66,12, con sus
   tres pilares y cada métrica ponderada.
4. **Los dos casos dorados anteriores se siguen probando con las bandas del BRD** (un ruleset
   de prueba): el motor reproduce el BRD escrito si se configura así. Con el ruleset de
   fábrica, la 14381 da **80,70** y la 10969 **80,26**.
5. **Las palancas del what-if** que llevaban OSS o Raw Tech a su mejor banda ahora los llevan
   a 100, el tope de la escala, cuando la métrica no tiene bandas.

## Alternativas consideradas

- **Mantener las bandas del BRD:** el sitio no coincidiría con el reporte que usa el negocio,
  que es contra lo que se valida.
- **Una versión de reglas aparte, v1.3-report:** dos «v1.3» con resultados distintos
  confundirían; el cambio va en la de fábrica y queda registrado aquí.

## Consecuencias

- Cambian los scores de casi todas las aplicaciones con OSS o Raw Tech informados. Con OSS
  bajo 60, el score sube; con Raw Tech de 75 o más (antes 100), baja un poco.
- El score de referencia de la 14381 pasa de 79,67 a **80,70**. La guía, el README y el
  generador de datos de ejemplo lo usan como comprobación de punta a punta.
- El editor de reglas no cambia el tipo de mapeo de una métrica: las bandas de OSS y Raw Tech
  ya no se editan. Las pruebas que editaban bandas usan ahora Operational Score.
- Una versión de reglas publicada antes del 2026-10-07 conserva las bandas. Para aplicar el
  cambio, se restaura la v1.3 de fábrica en **Rules → Versions** y se publica.
