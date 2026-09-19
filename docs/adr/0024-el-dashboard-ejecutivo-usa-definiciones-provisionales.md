# ADR 0024 — El dashboard ejecutivo usa definiciones provisionales, versionadas y dichas

**Estado:** aceptada, completada por el [ADR 0025](0025-las-definiciones-viajan-con-las-reglas.md) · **Fecha:** 2026-09-18

> **Actualización del mismo día (ADR 0025).** El Currency Score es el que entrega CMT
> (`Raw_Tech_Score`), no el porcentaje de componentes *Modern*; *legacy* arranca en 60. Las
> definiciones ya no se cambian desplegando: viajan con cada versión de reglas y se editan
> desde el portal. El dashboard es la página de inicio.

## Contexto

La propuesta *EA Data & Insights – MVP Proposal (DRAFT)* pide un dashboard para dirección,
*Portfolio Health & Risk Intelligence*, que responda una pregunta ejecutiva —dónde se
concentra el riesgo y la oportunidad de modernización, por qué, y qué decisión de
arquitectura hace falta— a través de las mismas seis preguntas de la pantalla 09
([ADR 0023](0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md)).

La propuesta fija la forma —indicadores arriba, un mapa de calor de capacidad primaria
contra región, país, CIO o disposición, una matriz criticidad × oportunidad, un treemap de
complejidad, un índice de concentración, niveles de resiliencia— pero deja buena parte de
las definiciones abiertas:

- «*Rules & Matrix to be defined globally*» para la matriz de la pregunta 2.
- «*Currency Score < X*» para *Legacy Platforms*, sin X ni Currency Score definido.
- «*See available concepts for Currency Scoring*» y «*… for Stability Scoring*» para las
  preguntas 5 y 6; esos conceptos no llegaron.
- Varias dimensiones de complejidad y de resiliencia en TBD, y fuentes que hoy no están
  conectadas: TLM (EOS/EOL), Architectural Assessments, ARB y Resiliency Data.

Había tres caminos: esperar a que el negocio cierre cada definición, inventar las que
faltan y presentarlas como definitivas, o implementar un default razonable que se pueda
cambiar sin desplegar y que la pantalla declare como provisional.

## Decisión

**Cada definición abierta es una regla con default, en un archivo versionado.**
`contracts/dashboard-v1.json` reúne los umbrales y agrupaciones: el corte de *legacy*, la
escala y los niveles del Risk Score, los cortes de oportunidad, cuándo una aplicación
tiene base amplia, los mínimos del cuadrante de candidatos, los umbrales de complejidad,
la parte de celdas que mide la concentración, los cortes de resiliencia y los grupos de
métricas que explican la obsolescencia. Se valida al cargar
(`DashboardRulesException`): un archivo incoherente no arranca el servicio. Lleva
`"status": "provisional"`, y la respuesta lo repite en `definitionsStatus`.

**El análisis es una función pura, junto a la de la pantalla 09.**
`DashboardAnalyzer.Analyze(aplicaciones, scores, planes, reglas, opciones)` vive en
`Modernization.Insights`, recibe lo que el motor ya calculó y no consulta nada. Mismo
portafolio, mismas reglas, mismo dashboard.

**Los defaults salen de lo que el modelo ya mide:**

| Concepto | Default | De dónde sale |
|---|---|---|
| Currency Score | % de componentes *Modern* sobre el total que reporta CMT | SR-02 ya lo proponía como KPI propio, fuera del Modernization Score |
| Legacy Platforms | Currency Score < 50 | La X del refinamiento |
| Risk Score (0–100, alto = peor) | Pilar *Modernization Risk Vectors* invertido: (100 − pilar) ÷ 80 × 100 | La propuesta asigna ese pilar a la pregunta 4 |
| Niveles de riesgo | < 40 bajo · 40–59 moderado · 60–79 alto · ≥ 80 crítico | Los del mockup |
| Criticidad (0–3) | Muy alta: crítica y de base amplia · Alta: crítica · Media: base amplia · Baja: el resto. Base amplia = base de usuarios significativa o tres o más capacidades | Los atributos que ya pondera la pantalla 09 |
| Oportunidad (0–3) | Uplift del roadmap propio: < 5 baja · 5–10 media · 10–20 alta · ≥ 20 muy alta; sin plan, sin oportunidad | El único impacto medido que existe |
| Modernization Candidates | Criticidad y oportunidad altas o muy altas | El cuadrante superior derecho del mockup |
| Índice de concentración | Parte del riesgo total que cargan el 20 % de las celdas con más riesgo; 0,4 / 0,6 / 0,8 separan los niveles | 0,2 es riesgo parejo; 1,0, todo en pocas celdas |
| Obsolescencia | Puntos perdidos por métrica, agrupados en conceptos; los de ciclo de vida y vigencia tecnológica cuentan como *currency* | Lo mismo que la pantalla 09, agrupado como pide la pregunta 5 |
| Resiliencia | Recuperación por DR y RTO (24 h y 48 h); vulnerabilidad operacional por score operacional (< 40 / 60 / 80) y monitoreo; el nivel de la aplicación es el peor de sus dimensiones | Los conceptos de Stability que ya tiene el modelo |

**Los indicadores son cinco, no siete.** Health Score, Apps at Risk y Modernization
Candidates, tal como los pide el refinamiento. EOS/EOL se redefine como exposición de
*currency* (parte de las aplicaciones con dato de CMT que son *legacy*) y Resilience como
parte de las aplicaciones con dato de resiliencia que tienen huecos altos o críticos.
Tech Debt queda a decisión; Governance Adoption queda fuera (N/A).

**Lo que no se puede medir se dice, no se estima.** Las dimensiones sin fuente —EOS, EOL,
disponibilidad, dependencias críticas, puntos únicos de falla, dependencias compartidas—
viajan con `status: "needs-data"` y números en null, y la pantalla las dibuja punteadas
con la fuente que las encendería. Las que se miden con una definición por confirmar viajan
con `status: "provisional"`. El pie lista qué fuentes están conectadas y cuáles no.

**Un detalle para todos los paneles.** Cada fila de `applications` lleva los mismos
campos —región, país, CIO, capacidad, disposición, score, riesgo, currency,
resiliencia— y las claves por las que se la selecciona. Una celda, una burbuja, una barra
o un indicador abren la misma tabla, filtrada distinto: eso pide el refinamiento de la
pregunta 1 para todos los wireframes.

**Los filtros son los del Modernization Score.** Se suman CRE, Region, App Status, Primary
Capability y EAI Code a los que ya existían, en las dos capas y en todas las pantallas con
filtro; viajan en la URL ([ADR 0016](0016-el-filtro-vive-en-la-url-y-solo-acota.md)) y el eje del mapa
de calor también (`axis`). El alcance se aplica sobre los datos
([ADR 0012](0012-el-alcance-se-aplica-sobre-los-datos.md)): la tendencia por carga usa el
mismo filtro y el mismo alcance que el resto.

## Consecuencias

- **El dashboard existe hoy y dice qué parte es provisional.** Nadie tiene que esperar a
  que se cierren todas las definiciones para ver el portafolio con esta forma, y nadie
  puede confundir un default con una definición acordada: la pantalla lo marca con un
  chip y la respuesta lo trae en `definitionsStatus`.
- **Cambiar un umbral es cambiar un dato.** Hoy requiere editar el JSON y desplegar; la
  fase D3 del plan lo lleva a las reglas versionadas del portal, con el mismo circuito de
  borrador, efecto y publicación.
- **Los datos dummy muestran que los defaults necesitan calibración.** Con 250
  aplicaciones, siete capacidades y tres países, *duplicate technologies* y *technology
  proliferation* marcan a todo el portafolio, la fila de criticidad «Alta» queda vacía
  (toda crítica es también de base amplia) y el 77 % tiene huecos de resiliencia. No son
  errores del cálculo: son umbrales pensados para un portafolio real, y la conversación
  con el negocio los tiene que fijar.
- **La oportunidad hereda el límite del ADR 0023.** Se mide con el uplift del roadmap, y
  el esfuerzo sigue siendo el del roadmap, no el de la aplicación (DQ-02).
- **El Risk Score no es el índice de riesgo de la pantalla 09.** Aquel suma conteos para
  ordenar grupos; este es una escala de 0 a 100 por aplicación, para poder dibujarla en un
  mapa de calor y en burbujas. Los dos conviven y cada pantalla dice cuál usa.
- **Riesgo asumido:** si el negocio define el Currency Score o la resiliencia con
  conceptos distintos de los del modelo, cambia el cálculo, no solo el umbral. El archivo
  de reglas absorbe los umbrales; un concepto nuevo es código nuevo en el analizador.
