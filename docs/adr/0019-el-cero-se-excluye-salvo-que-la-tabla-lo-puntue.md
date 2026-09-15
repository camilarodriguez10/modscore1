# ADR 0019 — El cero se excluye, salvo que la tabla del BRD lo puntue

**Estado:** aceptada · **Fecha:** 2026-09-14 · **Modifica:** [ADR 0003](0003-ausencia-de-dato-frente-a-valor-cero.md) · **Relacionado:** RFC-13

## Contexto

La seccion "ALL Sections" del BRD dice: *"Null or zero values are excluded from
scoring. All resulting scores are normalized within a 20 to 100 range, and null
values are not included in the normalization calculation."*

Hasta la Fase 12 el motor solo excluia el nulo. Un 0 caia en su banda: Kenna 0
puntuaba 100 y un OSS Score, RAW Tech Score u Operational Score de 0 puntuaban 20.
Eso contradecia la regla general del BRD.

La regla general choca con tablas especificas del mismo BRD, que nombran el 0 como
dato propio:

| Metrica | Tabla del BRD | Conflicto con "ALL Sections" |
|---|---|---|
| Tech Debt Score | `0 -> 100` | Si: el 0 tiene fila propia |
| SCA Flaws | `None -> 100` | Si: cero flaws es "None" |
| Automation DevSecOps | `0/NULL-3 -> 20` | Si, pero la metrica tiene peso 0 |
| MFA | `No (with no users) -> 100` | Si: cero usuarios es un caso |
| Kenna, OSS, RAW Tech, Operational | `0-339`, `0-59`, `0-59`, `0-59,9` | No: el 0 solo es el borde de una banda |
| Architectural Value, Product Strategy | promedio por conteo | No: sin productos ya se excluye |

## Decision

1. La regla especifica gana sobre la general. Donde la tabla del BRD nombra el 0
   (o su equivalente "None", "no users"), el 0 puntua segun la tabla.
2. Donde el 0 solo es el borde inferior de una banda, se aplica "ALL Sections": el 0
   sale del numerador y del denominador, igual que un nulo.
3. La regla vive en el ruleset, no en el codigo: cada metrica declara
   `"zeroIsNoData": true|false`. En v1.3 queda en `true` para `kenna`, `oss`,
   `rawTech` y `operational`. El validador rechaza la marca en metricas que no
   usan bandas numericas.
4. La exclusion por cero tiene su propio motivo (`exclusion.zeroValue`), distinto de
   "no reportado", para que la pantalla de explicacion diga lo que paso.

## Consecuencias

- Los casos dorados (EAI 14381 = 79,67; EAI 10969 = 79,61) no cambian: ninguno
  tiene un 0 en esas metricas.
- Una aplicacion con Kenna 0 ya no recibe el mejor puntaje de riesgo de host por un
  valor que en la practica suele significar "sin escaneo". Su denominador baja.
- En los datos dummy hay una aplicacion con RAW Tech Score 0; su score cambia.
- Una version publicada desde el portal antes de este cambio no trae la marca y se
  interpreta como `false`. Solo afecta a entornos de prueba: no hay versiones
  productivas publicadas.
- **Pendiente de confirmar (RFC-13):** que el negocio lea "zero values are excluded"
  igual. Si el modelo Power BI puntua el 0 de Kenna u OSS, basta con publicar una
  version con la marca en `false`; no hay que recompilar.
