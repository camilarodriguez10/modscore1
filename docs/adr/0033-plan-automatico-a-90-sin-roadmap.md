# ADR 0033 — Plan automático a 90 para las aplicaciones sin roadmap de MAPS

**Estado:** aceptada · **Fecha:** 2026-10-07 · **Relacionado:** [ADR 0024](0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md), [ADR 0025](0025-las-definiciones-viajan-con-las-reglas.md), [ADR 0032](0032-oss-y-raw-tech-con-valor-bruto.md)

## Contexto

La pregunta 2 del dashboard (*Which applications should be modernized first?*) cruza la
criticidad con la **oportunidad de modernización**, que eran los puntos que suma al score el
roadmap de MAPS de cada aplicación (DD-03). Sin roadmap no había plan, y la aplicación quedaba
fuera de la matriz.

El export real del EAI Master no trae la columna `Roadmap`. Con los datos de la organización,
la pregunta 2 quedaba vacía, sin decir por qué. El 2026-10-07 se pidió activar, después de
cada carga, planes automáticos hacia un score de 90.

## Decisión

1. **Plan automático.** Una aplicación sin roadmap de MAPS recibe un plan hacia el objetivo
   del catálogo de roadmaps (`automaticPlanTarget`, **90**): las palancas del what-if de
   mayor efecto individual, una tras otra, hasta que el motor confirma el objetivo. Si 90 no
   se alcanza, quedan todas las palancas que suman. Si ya está en 90 o más, el plan no mueve
   nada (oportunidad baja).
2. **No para lo que se va a eliminar.** Una aplicación marcada *Eliminate* sin roadmap no
   recibe plan: proyectar una mejora ahí justificaría invertir en algo que se apaga. Cuenta
   como fuera del portafolio.
3. **Es un plan más.** Lo arma el planificador de roadmaps, así que lo ven el dashboard, la
   pantalla Roadmap («No MAPS roadmap: an automatic plan with 6 levers takes the score from
   62.32 to 90.88…») y los promedios del roadmap del portafolio. No tiene hitos ni semanas, así
   que no entra en las prioridades de Insights, que ordenan por puntos por semana. Los roadmaps
   de MAPS siguen igual y tienen prioridad.
4. **El objetivo es dato.** `automaticPlanTarget` vive en `contracts/roadmap-v1.json`, con
   validación de 20 a 100; null apaga los planes automáticos. Un catálogo publicado antes del
   2026-10-07 no lo trae y los deja apagados.
5. **Costo acotado.** `WhatIfEngine.PlanToTarget` es la búsqueda del laboratorio sin la poda,
   unas cinco veces más barata. El plan se guarda por aplicación y reglas mientras la carga
   siga en memoria: medido con 4 000 aplicaciones, la primera vez ~2 s y las siguientes 68 ms.
6. **La pantalla dice qué pasó.** El top 10 marca *automatic plan to 90* en lugar del roadmap.
   El pie cuenta cuántas usan plan automático, y si la matriz queda vacía (planes automáticos
   apagados y sin roadmaps) explica qué falta y lleva a *Data → Mapping*.

## Alternativas consideradas

- **Exigir la columna Roadmap:** depende de un export que hoy no la trae.
- **La búsqueda mínima del laboratorio (`ReachTarget`) para todo el portafolio:** unos 3 s más
  por cada carga del dashboard con 3 900 aplicaciones, sin caché.
- **Oportunidad = 100 − score:** no dice qué hacer. El plan automático dice qué palancas y
  cuánto suma cada una.

## Consecuencias

- Con el export sin roadmap, la pregunta 2 se llena: en la prueba en vivo, 172 de 250
  aplicaciones con plan automático y 35 candidatas; las 78 restantes, marcadas Eliminate.
- La oportunidad de una aplicación sin roadmap mide lo que falta para llegar a 90 con las
  palancas disponibles, no la estrategia que el negocio eligió para ella. Cuando el export
  traiga el roadmap de MAPS, manda el roadmap.
- El objetivo todavía no se edita desde *Rules*: cambiarlo es publicar un catálogo nuevo.
