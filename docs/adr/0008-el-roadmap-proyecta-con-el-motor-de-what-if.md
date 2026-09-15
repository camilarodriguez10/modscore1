# ADR 0008 — El roadmap proyecta con el motor de what-if

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

El requisito 6 pide un plan futuro por aplicación en función de su roadmap y su
disposition. La parte fácil son los hitos y las fechas. La difícil es el número:
¿en cuánto queda el score al terminar?

La respuesta tentadora es escribirlo a mano — una tabla que diga "Refactor sube
15 puntos". Esa tabla se desincroniza del modelo de scoring la primera vez que
alguien cambia un peso, y nadie se entera.

## Decisión

Cada roadmap **declara qué palancas del what-if entrega su ejecución**, en
`contracts/roadmap-v1.json`, con la justificación de por qué esas y no otras.
El score proyectado sale de aplicar esas palancas al motor de what-if.

Ejemplos, derivados de las definiciones del propio BRD:

| Roadmap | Palancas | Por qué |
|---|---|---|
| Maintain (Patch) | remediar SCA crítico y alto, subir OSS, bajar Kenna | "Mantener vigente y seguro" es exactamente eso. No toca arquitectura. |
| Rehost | monitoreo, operational | La plataforma destino trae observabilidad. El BRD dice "sin recompilación", así que la arquitectura y la deuda quedan igual. |
| Refactor | APIM, modernizar productos, deuda técnica, RAW Tech, SCA | Tocar la estructura es la oportunidad de modernizar. |
| Retire · Consolidate | ninguna | La aplicación sale del portafolio. |

## Consecuencias

- **Una sola fuente de verdad.** Si cambia un peso del ruleset, la proyección
  cambia con él. No hay una segunda tabla que mantener sincronizada.
- **El número viene con su desglose.** La pantalla no dice "+12,89": dice qué
  cuatro palancas lo producen y qué acción real representa cada una.
- **Retire y Consolidate no proyectan mejora.** Su score no sube — deja de
  existir. Proyectar una ganancia ahí justificaría invertir en algo que se va a
  apagar. La UI lo dice con esas palabras.
- **Los números resultantes son informativos por sí solos.** En el portafolio
  dummy: Maintain (Patch) gana +15,13 en 14 semanas; Reimagine gana +25,69 en 60.
  Cuatro veces la duración por menos del doble de ganancia — exactamente el tipo
  de comparación que una decisión de portafolio necesita.
- **Los umbrales de las reglas de coherencia también son datos.** `healthyScore`
  y `significantUserBase` viven en el contrato, no como constantes en el código.
- **Riesgo asumido.** El mapeo roadmap → palancas es una interpretación de las
  definiciones del BRD, no algo que el BRD declare. Está documentado en el
  contrato con su justificación (`leverRationale`) para que sea discutible y
  ajustable sin tocar código. **Vale la pena validarlo con los dueños del modelo.**
