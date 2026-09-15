# ADR 0002 — Las reglas de scoring son datos, no codigo

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

El BRD y el dashboard v1.3 discrepan en dos pesos (Tech Debt: 5 contra 10) y hay
cinco supuestos abiertos (RFC-01 a RFC-05). Los pesos y las bandas van a cambiar
mas de una vez antes de estabilizarse, y cada cambio debe ser auditable: hay que
poder decir con que reglas se calculo un score de hace tres meses.

## Decision

Los pesos, las bandas y los puntajes por categoria viven en
`contracts/ruleset-v1.3.json`. La extraccion del valor crudo desde el snapshot
sigue siendo codigo tipado; el mapeo a puntaje y la ponderacion son datos.

Los rulesets son inmutables y versionados. Cada `ScoreBreakdown` registra
`RulesetVersion`.

## Consecuencias

- Cambiar un peso es publicar un ruleset nuevo, no recompilar ni desplegar.
- El ruleset se valida al cargarse: pesos que no suman su pilar, metricas que
  apuntan a un pilar inexistente o inputs desconocidos fallan de inmediato.
  Un ruleset incoherente produciria scores silenciosamente incorrectos, que es
  el peor modo de falla de este sistema.
- Un valor que no cae en ninguna banda lanza excepcion en vez de puntuar mal.
  El error nombra la metrica y el valor. Falla por aplicacion, no por lote.
- El ruleset viaja embebido en el ensamblado: la version queda atada al binario
  desplegado y el motor no depende del layout del disco.
