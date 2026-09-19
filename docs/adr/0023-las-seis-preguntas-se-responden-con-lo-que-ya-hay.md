# ADR 0023 — Las seis preguntas se responden con lo que ya hay

**Estado:** aceptada · **Fecha:** 2026-09-16

## Contexto

El portafolio tiene que poder responder seis preguntas de gestión:

1. ¿qué tan sano está?
2. ¿qué aplicaciones modernizar primero, según costo/esfuerzo e impacto?
3. ¿dónde reducir complejidad?
4. ¿dónde se concentra el riesgo tecnológico?
5. ¿qué está causando la obsolescencia?
6. ¿dónde están los huecos de resiliencia?

Las seis se pueden responder con las ocho fuentes actuales, el score ya calculado y el
catálogo de roadmaps. Ninguna necesita un dato nuevo **para existir**; varias mejoran
mucho con datos que hoy no están (ver *Consecuencias*).

Había dos caminos. Uno: una pantalla por pregunta, cada una con su consulta. Otro: un
solo análisis que responda las seis sobre el mismo recorte. Se eligió el segundo, porque
las seis preguntas se hacen juntas —*"esto está mal, ¿por qué, dónde y qué hago
primero?"*— y porque responderlas por separado invita a que cada una use un filtro
distinto y las respuestas no cuadren entre sí.

## Decisión

**Un proyecto compartido, `Modernization.Insights`, con una función pura.**
`InsightsAnalyzer.Analyze(aplicaciones, scores, planes, opciones)` recibe lo que el motor
ya calculó y devuelve las seis respuestas. No consulta nada, no decide nada y no redacta
frases: ordena y cuenta. Mismo portafolio, mismo resultado.

**Cada número trae su aritmética.** No hay ponderaciones ocultas:

| Pregunta | Cómo se responde |
|---|---|
| Salud | Promedio y mediana de los scores, reparto por tramo, cobertura de dato (peso del modelo que tuvo valor) y cuántas críticas están bajo el umbral sano del catálogo de roadmaps. |
| Prioridad | `uplift` = score proyectado − score actual, con el motor de what-if sobre el roadmap de la aplicación. `weeks` = suma de las semanas de sus hitos. `businessWeight` = 1 + 1 si es crítica + 0,5 si supera la base de usuarios significativa + 0,25 si cubre tres o más capacidades. `impact` = uplift × peso; el orden es `impact ÷ weeks`. |
| Complejidad | Cuatro formas: misma capacidad y mismo país a partir de N aplicaciones; misma capacidad resuelta en N países; marcadas `Eliminate` y todavía activas; una capacidad escrita en N lenguajes. N es ajustable desde la pantalla (2 a 5). |
| Riesgo | Por LOB, país, CIO y capacidad. `índice` = críticos×3 + altos×1 + Kenna≥670×5 + componentes obsoletos o no aprobados×2 + críticas expuestas a internet×10. Hasta el 2026-09-18 decía Kenna<50, al revés de la escala de Kenna (más alto es peor); se corrigió en el ADR 0025, y desde entonces todos estos pesos son parámetros editables. |
| Obsolescencia | Por métrica con peso: `puntos perdidos` = peso × (100 − score mapeado) ÷ 100, sumado en todo el portafolio. El peso que quedó fuera por falta de dato va en su propia columna. |
| Resiliencia | Fuera del alcance de DR, sin monitoreo, score operacional bajo 60, sin RTO o RPO declarados. Los dos primeros contadores miran solo aplicaciones críticas. |

**El esfuerzo es el del roadmap, y se dice.** El único esfuerzo que existe en los datos
de hoy son las semanas de los hitos del catálogo MAPS, que son por roadmap, no por
aplicación: dos aplicaciones con el mismo roadmap tienen las mismas semanas. Es un
*proxy*, no una estimación. Se usa igual —ordenar por valor por semana es mejor que
ordenar por uplift a secas, que siempre premia a la aplicación más grande— y la pantalla
lo dice con esas palabras.

**Los umbrales son datos, no constantes.** `healthyScore` y `significantUserBase` salen
del catálogo de roadmaps, que se edita desde el portal: cambiar ahí qué es "sano" cambia
también este análisis, sin desplegar. Los dos umbrales propios de la pantalla —a partir
de cuántas aplicaciones una coincidencia es concentración, y cuántas filas trae cada
lista— viajan en la URL y la capa System los acota (2–50 y 1–50).

**El filtro manda.** El análisis usa los mismos filtros que el inventario y se calcula
sobre lo que el alcance del usuario deja ver ([ADR 0012](0012-el-alcance-se-aplica-sobre-los-datos.md)):
lo que alguien no puede ver no entra en ningún promedio suyo.

**La evidencia viaja como código, no como frase.** Cada candidato, concentración y hueco
lleva sus `Notice` con datos, y la interfaz arma la oración
([ADR 0017](0017-el-backend-no-redacta-frases.md)).

## Consecuencias

- **Una llamada, seis respuestas coherentes entre sí.** Todas hablan del mismo recorte y
  de la misma versión de reglas, que viaja en la respuesta.
- **Las respuestas son discutibles.** Cualquiera puede mirar la fórmula del índice de
  riesgo o del peso de negocio y decir "el peso de una crítica debería ser otro". Eso es
  lo que se busca: que la conversación sea sobre el criterio, no sobre si el número
  salió de una caja negra.
- **El índice de riesgo ordena dentro de un portafolio, no entre portafolios.** Suma
  conteos con pesos elegidos; no es una escala absoluta ni compara con otra empresa.
- **La complejidad no dice "esto sobra".** Dice dónde hay varias piezas resolviendo lo
  mismo, con quiénes son, para que alguien que conoce el negocio lo confirme o lo
  descarte. Dos aplicaciones para la misma capacidad en dos segmentos de cliente pueden
  ser correctas.
- **Falta de dato no es obsolescencia.** Una métrica sin valor no resta puntos: resta
  certeza. Por eso el peso sin dato va en una columna aparte y la pantalla lo nombra.
- **Riesgo asumido: el esfuerzo no es real.** Mientras el esfuerzo sea el del roadmap y
  no el de la aplicación, el orden de la lista de prioridades es una hipótesis razonable,
  no un plan de inversión. Los datos que lo convertirían en uno están en
  [docs/PENDING-ITEMS.md](../PENDING-ITEMS.md) como **DQ-01 a DQ-07**; el más barato de
  conseguir y el que más cambia la respuesta es el costo o el esfuerzo estimado por
  aplicación (DQ-02).
- **El ejemplo del negocio —"más de X aplicaciones con la misma *application class* por
  país"— se responde hoy con la capacidad de negocio primaria** (`L1 Business Capability`
  del maestro EAI), que es lo más cercano que existe en las fuentes. Con una columna
  explícita de *application class* (DQ-01) la misma pantalla respondería la pregunta tal
  como está formulada, sin cambiar el análisis.
