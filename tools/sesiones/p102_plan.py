from patchlib import patch

patch("PLAN.md", [
# Mapa de pantallas
("""| 9 | **Insights** | Las seis preguntas de gestión sobre el portafolio filtrado: salud, qué modernizar primero (impacto contra esfuerzo), dónde reducir complejidad, dónde se concentra el riesgo, qué causa la obsolescencia y dónde faltan defensas de continuidad. Cada número con su aritmética a la vista ([ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md)). | 4, 6 |""",
"""| 9 | **Insights** | Las seis preguntas de gestión sobre el portafolio filtrado: salud, qué modernizar primero (impacto contra esfuerzo), dónde reducir complejidad, dónde se concentra el riesgo, qué causa la obsolescencia y dónde faltan defensas de continuidad. Cada número con su aritmética a la vista ([ADR 0023](docs/adr/0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md)). | 4, 6 |
| 10 | **Code** | Obsolescencia y riesgo según lo que declaran los repositorios de cada aplicación en GitHub Enterprise: componentes contra su fecha de fin de vida (endoflife.date), bibliotecas obsoletas, desactualizadas o con alertas, discrepancias con el Currency Score de CMT e índice de riesgo de código por aplicación, con su detalle. Datos de demostración hasta la integración ([ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)). | — |"""),
# Tabla de fases
("""| Dashboard ejecutivo — D4 | PLANIFICADA | Fuentes nuevas: TLM, resiliencia, deudas técnicas, application class, dependencias | Cada fuente encendida convierte un bloque «sin dato» en una medición |""",
"""| Dashboard ejecutivo — D4 | PLANIFICADA | Fuentes nuevas: TLM, resiliencia, deudas técnicas, application class, dependencias | Cada fuente encendida convierte un bloque «sin dato» en una medición |
| **Pantalla Code — C1** ✅ | ENTREGADA | Pedida el 2026-09-26: obsolescencia y riesgo por librerías, software y componentes de cada repositorio, con endoflife.date como fuente del fin de vida y datos de demostración hasta integrar GitHub Enterprise (ver 8.11) | **Cumplido:** la pantalla responde con los mismos filtros y alcance, cada número se explica con las definiciones vigentes (área `code`, 14 parámetros editables) y el proveedor de demostración no arranca fuera de Development |
| Pantalla Code — C2 · GitHub Enterprise | PLANIFICADA | El recolector `code-collector` con una GitHub App de solo lectura, el mapeo repositorio → EAI y la publicación del inventario a la capa System (ver 8ter, GH-01 a GH-09) | La pantalla muestra repositorios reales de una organización piloto, sin ningún dato de demostración y sin que la System ni la Channel tengan credenciales de GitHub |"""),
# Seccion 8.11, antes de 8bis
("""## 8bis. Ampliación solicitada — fases 10 a 12""",
"""### 8.11 Pantalla Code — obsolescencia y riesgo desde los repositorios (2026-09-26)

**El pedido:** analizar el código de las aplicaciones directamente desde GitHub Enterprise,
sin configurar todavía la integración; dejarla en el plan y, con lo que se puede obtener de
cada repositorio, construir una vista de obsolescencia y riesgo según las librerías, el
software y los componentes, con una fuente para el fin de vida.

**C1, entregada el mismo día ([ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md)).**

| Pregunta de la pantalla | Qué muestra |
|---|---|
| ¿Qué componentes llegaron o están por llegar a su fin de vida? | Cada runtime, framework, servidor, imagen de sistema operativo y base de datos que declaran los repositorios, por ciclo, con su fecha, su estado y el enlace a endoflife.date; una barra por estado que filtra la tabla |
| ¿Qué bibliotecas necesitan atención? | Las que están obsoletas, varias versiones mayores atrás o con alertas críticas y altas, con cuántas aplicaciones las usan |
| ¿Dónde el código contradice al inventario? | *Hidden obsolescence*: vigente según el Currency Score de CMT, con componentes vencidos en el código. *Inventory behind the code*: legada según CMT, sin nada vencido en el código |
| ¿Qué aplicaciones cargan más riesgo de código? | El índice de riesgo de código y su nivel; el detalle de cada aplicación muestra sus repositorios y de dónde sale cada punto |

- **Fuente del fin de vida:** endoflife.date, como foto versionada en
  `contracts/eol-catalog-v1.json` (22 productos, 273 ciclos, consultada el 2026-09-26). El
  soporte extendido pagado se muestra, pero no cuenta como soporte.
- **Reglas:** el área `code` de las definiciones, con 14 parámetros editables en
  *Rules → Portfolio analytics* y explicados en *Definitions*. El índice no entra al
  Modernization Score.
- **Datos de demostración:** generados del portafolio vigente, deterministas por EAI;
  el lenguaje del maestro elige la familia tecnológica y el Currency Score inclina las
  versiones. COBOL y ABAP casi nunca tienen repositorio, como en la realidad. La pantalla
  lo dice en ámbar, y ese proveedor no arranca fuera de Development.
- **Pruebas:** 39 más en el motor, 5 en las fuentes, 14 en la API, 5 en Vitest y 5
  escenarios e2e, más axe en la pantalla y en el detalle, en claro y en oscuro.

**C2 — la integración con GitHub Enterprise** está en [8ter](#en-paralelo--integración-con-github-enterprise-pantalla-code)
y en GH-01 a GH-09 de [docs/PENDING-ITEMS.md](docs/PENDING-ITEMS.md).

## 8bis. Ampliación solicitada — fases 10 a 12"""),
# 8ter: via paralela
("""### Fase 17 — Hallazgos de infraestructura (al final, por decisión)""",
"""### En paralelo — Integración con GitHub Enterprise (pantalla Code)

La pantalla Code ya existe con datos de demostración (8.11). Esta es la vía para llenarla
con repositorios reales, según el [ADR 0026](docs/adr/0026-el-analisis-de-codigo-llega-de-github-enterprise.md).
No depende de las fases 14 a 16 salvo en el destino de despliegue (PA-01), y puede empezar
en cuanto se aprueben la GitHub App y el mapeo.

| Trabajo | Puntos |
|---|---|
| Aprobar la GitHub App: organizaciones, permisos de solo lectura (*Metadata*, *Contents*, *Dependabot alerts*, *Custom properties*), versión de GitHub Enterprise que expone el SBOM y las alertas | GH-01 |
| Convención de mapeo repositorio → aplicación: propiedad personalizada `eai-code`, y cómo se tratan los monorepos y las bibliotecas compartidas | GH-02 |
| `code-collector` como tarea programada, sin ingress: lee metadatos, SBOM, alertas y la lista cerrada de manifiestos; no clona ni ejecuta nada | GH-01, GH-03 |
| Salida de red: endoflife.date y registros de paquetes, directos o por espejos internos | GH-03 |
| Fuente de vulnerabilidades: Dependabot, OSV.dev o ambas | GH-04 |
| Endpoint interno de la capa System para publicar el inventario: token de servicio, rol propio, esquema, límites, versionado y retención como una carga | — |
| Refresco del catálogo de fin de vida y excepciones por contrato de soporte extendido | GH-06 |
| Acordar los puntos y niveles del índice (área `code`) y marcarla como *agreed* | GH-07 |
| Qué hacer con lo que no vive en GitHub (COBOL, ABAP) | GH-08 |
| Circuito con los dueños de CMT cuando el código contradice al Currency Score | GH-09 |
| Imágenes de contenedor: escanear lo que corre, no solo lo que se declara | GH-05 |

**Criterio de aceptación:** la pantalla Code muestra los repositorios reales de una
organización piloto, con el proveedor de demostración apagado; ni la capa System ni la
Channel tienen credenciales de GitHub; un repositorio sin mapeo aparece en la lista de
huérfanos y no en ningún conteo; y el catálogo se refresca solo, conservando el anterior si
el nuevo no valida.

### Fase 17 — Hallazgos de infraestructura (al final, por decisión)"""),
])
