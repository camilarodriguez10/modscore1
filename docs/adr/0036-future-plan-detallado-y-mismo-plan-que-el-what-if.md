# ADR 0036 — Future plan detallado al cargar, y el mismo plan que el what-if

**Estado:** aceptada · **Fecha:** 2026-10-21 · **Relacionado:** [ADR 0033](0033-plan-automatico-a-90-sin-roadmap.md), [ADR 0017](0017-el-backend-no-redacta-frases.md)

## Contexto

El 2026-10-21 se pidió:

- que al cargar información se genere de manera automática el *future plan* de cada
  aplicación, con el máximo detalle posible de las acciones para mejorar el score;
- que el plan coincida con el del what-if. Si se pide 90 en el what-if, el resultado tiene
  que ser el *future plan* propuesto.

Desde el ADR 0033, el plan automático usaba una búsqueda sin poda (`PlanToTarget`) y el
what-if usaba otra con poda (`ReachTarget`). Podían diferir en una palanca. Las palancas de
productos entraban siempre con cantidad 1, así que a veces el what-if declaraba alcanzable un
objetivo que su propio plan no alcanzaba. El plan, además, solo decía qué palancas aplicar.

## Decisión

1. **Una sola búsqueda.** `WhatIfEngine.ReachTarget` es el plan de los dos. `PlanToTarget`
   desaparece.
   1. Toma cada palanca que suma sola y a su máximo, de mayor a menor ganancia. Las de
      productos toman todos los que pueden mover (`ScenarioLever.Capacity`).
   2. Las agrega en ese orden, verificando con el motor, hasta alcanzar el objetivo.
   3. Poda las que sobran al recalcular en conjunto.
   4. Baja cada palanca de productos a la cantidad mínima que sostiene el objetivo
      (búsqueda binaria; tope de seguridad `MaxPlannedProducts`).

   Si ni con todas se alcanza, el plan son todas las que suman, a su máximo, y los dos lo
   dicen igual. Es determinista: a igual ganancia ordena por clave.
2. **Un plan para cada aplicación, tenga o no roadmap de MAPS.**
   - `RoadmapPlanner.TargetPlan` guarda por aplicación y reglas el plan hacia
     `automaticPlanTarget` (90); las marcadas Eliminate no tienen.
   - Sin roadmap de MAPS, ese plan es la proyección, como antes.
   - Con roadmap, la proyección sigue siendo la del roadmap, y el plan a 90 se muestra aparte.
3. **Se calcula al cargar.** `PortfolioWarmup`, que ya corre al arrancar y después de cada
   carga, llama a `RoadmapPlanner.Prepare` con todo el portafolio.
4. **Detalle de cada paso.** `ActionPlanBuilder` aplica las palancas en el orden del plan y
   arma un paso por cada una (`ActionStep`). Cada paso dice:
   - la métrica, con su valor y su puntaje antes y después;
   - el peso que entra al cálculo;
   - los puntos que suma después de los anteriores y el score que deja;
   - quién lo ejecuta (Security, Infrastructure, Development team, Enterprise architecture u
     Operations);
   - las acciones concretas, como avisos con datos (la interfaz redacta la frase, ADR 0017):
     - en SCA, los hallazgos abiertos más graves, con librería, proveedor, CVE, CVSS y días
       abiertos; el snapshot guarda hasta diez críticos y diez altos por aplicación
       (`ApplicationSnapshot.OpenScaFindings`);
     - Kenna, OSS, Raw Tech, el score operacional y la deuda técnica, desde y hasta qué valor;
     - qué productos modernizar o reclasificar, por categoría;
     - qué capacidades de monitoreo faltan;
     - el RTO y el RPO declarados para el plan de DR;
     - los usuarios a cubrir con MFA;
     - la exposición actual de API.

   El score después del último paso es exactamente el del what-if.
5. **Se abre en el what-if.**
   - La pantalla *Future plan* muestra *Action plan to reach 90* con un botón *Open in
     What-If*. El botón carga en el laboratorio las mismas palancas con las mismas cantidades
     (`?plan=` validado por forma).
   - El laboratorio parte del objetivo del catálogo (`automaticTarget`), y *Load this plan
     into the levers* conserva las cantidades.

## Alternativas consideradas

- **Mantener las dos búsquedas y comparar:** dos algoritmos que «deberían» dar lo mismo
  terminan divergiendo. Uno solo lo garantiza por construcción.
- **Calcular el plan solo cuando alguien lo abre:** el pedido es que esté al cargar. Además,
  el dashboard ya lo necesita para la oportunidad de la pregunta 2.
- **Un texto libre por palanca:** sin los datos de la aplicación (qué CVE, cuántos productos,
  qué capacidades), no dice qué hacer.

## Consecuencias

- **Coincidencia probada.** Las pruebas cubren el motor, las dos capas (doce aplicaciones:
  plan, what-if a 90 y escenario con las palancas del plan) y la pantalla de punta a punta.
- **Números del dashboard.** La oportunidad de las aplicaciones sin roadmap puede bajar un
  poco frente al ADR 0033: la poda quita palancas que sobraban.
- **Costo.** La búsqueda es la del laboratorio (≈0,7 ms por aplicación) más la poda de
  cantidades. Con el portafolio en memoria se hace una vez por aplicación y reglas.
- **Datos de ejemplo.** Los nombres de vulnerabilidad del portafolio dummy pasaron al inglés,
  como los exporta Veracode, y `data/dummy` y `data/timeline` se regeneraron.

## Actualización del 2026-10-21: el objetivo elegido viaja al future plan

Probando el sitio se vio que, con el What-If a 80, el future plan seguía mostrando el plan a
90. La coincidencia valía solo para el objetivo del catálogo. Ahora el objetivo viaja en la URL
(`?target=`), en las dos direcciones:

- El future plan hacia otro objetivo se calcula a pedido con la misma búsqueda (`ReachTarget`).
  Por eso coincide con *Find the minimum plan* a ese objetivo.
- El objetivo del catálogo sigue saliendo del plan preparado al cargar.
- Una aplicación marcada *Eliminate* no tiene plan con ningún objetivo.
- Una con roadmap de MAPS conserva la proyección de su roadmap; el objetivo cambia su plan de
  acción.
