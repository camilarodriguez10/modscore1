# ADR 0007 — El what-if recalcula, no suma deltas

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

La forma barata de simular un escenario es calcular el delta de cada palanca una
vez y sumarlos. Con la fórmula del BRD, eso da resultados incorrectos.

La razón está en el denominador. Una métrica sin dato queda fuera del numerador
**y del denominador**; una palanca que aporta dato a esa métrica cambia las dos
partes de la división. El delta de una palanca depende entonces de qué otras
palancas se apliquen con ella.

## Decisión

Cada escenario aplica las palancas sobre una copia del snapshot y **recalcula
desde cero** con el mismo `ScoringEngine` que produce el score real.

El resultado expone las tres cifras juntas: el delta conjunto, la suma de los
deltas individuales, y la diferencia entre ambos. Cuando esa diferencia no es
cero, la respuesta la explica en texto.

## Consecuencias

- **El caso real, medido.** En el EAI 14381, tres palancas que aportan dato nuevo
  suben el peso del cálculo de 60 a 80 y dan **+13,08 puntos en conjunto**;
  sumando los deltas individuales saldría 16,46. Un 25 % de error, en la
  dirección optimista, que es la peor.
- **La ganancia es verificable a mano.** Cuando la métrica ya tiene dato, la regla
  es `Δ = 0,8 × peso × (valor′ − valor) ÷ denominador`. Con un denominador de 80,
  habilitar MFA vale exactamente 5,60 puntos, y hay un test que lo comprueba
  contra la fórmula además de contra el motor.
- **Coste.** Un catálogo de 13 palancas son 13 recálculos por aplicación. El motor
  es puro y opera sobre estructuras en memoria, así que el coste es despreciable
  frente a la corrección que compra.
- **Nunca muta el snapshot real.** Los escenarios operan sobre copias y hay un
  test end-to-end que verifica que el score de la aplicación no cambia después de
  simular. Si simular fuera destructivo, nadie lo usaría dos veces.
- **Una palanca puede no ser simulable.** Cuando la fuente entrega el compuesto ya
  calculado en lugar del detalle por producto, no hay conteos que reclasificar: se
  reporta el motivo en vez de dejar al usuario preguntándose por qué la palanca no
  hace nada. Es también un argumento para pedirle a CMT el detalle.
