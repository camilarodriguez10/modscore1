# ADR 0025 — Las definiciones de las seis preguntas viajan con las reglas y se editan desde el portal

**Estado:** aceptada · **Fecha:** 2026-09-18 · **Completa:** [ADR 0024](0024-el-dashboard-ejecutivo-usa-definiciones-provisionales.md)

## Contexto

El ADR 0024 dejó cada definición abierta del dashboard como un default en
`contracts/dashboard-v1.json`: cambiar un umbral exigía editar el archivo y desplegar. El
mismo día el negocio pidió tres cosas:

1. **Currency Score es el score que viene de CMT.** No el porcentaje de componentes
   *Modern* que se había supuesto.
2. **El dashboard es la página de inicio.**
3. **Los cálculos de las seis preguntas tienen que ser paramétricos y configurables desde
   el sitio, en modo administrador**, y el sitio tiene que explicar qué considera cada
   respuesta y con qué reglas se calcula.

La pantalla Insights (ADR 0023) también tenía números fijos en el código: los pesos del
índice de riesgo, el peso de negocio, el corte de Kenna y el del score operacional.

## Decisión

**Currency Score = `Raw_Tech_Score` del Currency Model Detail Report de CMT.** Es el único
score que CMT entrega hoy (0–100, calculado por componente a partir de su fecha de fin de
vida) y ya es una métrica del modelo, *RAW Tech Score*. Un 0 es falta de dato, igual que en
el score (ADR 0019). *Legacy* = Currency Score por debajo de X; X arranca en **60**, donde
termina la banda más baja del BRD (0–59). Si CMT entrega otra columna con ese nombre, se
agrega como fuente sin cambiar la definición.

**Las definiciones son una cuarta parte de cada versión de reglas.** `RulesModel` ya
versionaba juntos ruleset, palancas y roadmap porque dependen entre sí; las definiciones
también dependen de ellos —el pilar que se invierte para el riesgo, las métricas que
explican la obsolescencia, la base de usuarios significativa— y se suman al mismo
paquete. Con eso heredan, sin código nuevo de infraestructura, lo que ya tenía la
administración de reglas: borrador, simulación sobre todo el portafolio, publicación con
motivo, reautenticación, auditoría, versiones inmutables, comparación, vuelta atrás y
refresco entre réplicas sin desplegar. Una versión publicada antes de este cambio no trae
definiciones y usa las de fábrica. En la base de datos es una columna nueva, con
migración.

**Un catálogo único de parámetros.** `AnalyticsCatalog` lista cada número editable —37 hoy,
en siete áreas— con su clave, su tipo, su rango y sus decimales, y las reglas entre ellos
(niveles en orden). Esa lista valida al cargar y al editar, arma el documento que ve el
administrador y el que explica los cálculos. Un parámetro nuevo se agrega en un solo
lugar y la pantalla de administración lo muestra sin cambios. Cada error vuelve como aviso
con código —valor fuera de rango, regla rota, área desconocida— y un valor desconocido se
informa por posición, sin repetir lo que se envió.

**Cada área tiene estado y dueño.** *Provisional* o *agreed*, y quien responde por ella. El
dashboard deja de llamar provisional a un área cuando el negocio la marca como acordada.

**Lo que se edita y lo que no.** Se editan umbrales, niveles, cuántas filas lista cada
panel y los pesos de Insights. No se edita de dónde sale un dato ni qué métrica explica qué
concepto: eso es código, y cambiarlo sin desplegar daría números silenciosamente
equivocados (el mismo criterio del ADR 0018).

**Las explicaciones se arman con los valores vigentes.** El dashboard e Insights reciben las
definiciones con sus datos, y cada panel explica qué considera, cómo se calcula con los
números que de verdad usó y qué falta medir. La pantalla *Definitions* es el diccionario
de indicadores que pedía el paso 3 de la propuesta: definición y fórmula, datos, valores,
dueño, estado, frecuencia y cobertura real de datos.

**El dashboard es la página de inicio** (`/`); el inventario pasa a `/portfolio` y
`/dashboard` redirige conservando los filtros.

**El impacto de una definición se mide en el dashboard.** Una definición no mueve ningún
score. La simulación lo dice —cero aplicaciones que suben o bajan— y muestra los cinco
indicadores del dashboard antes y después, sobre todo el portafolio. Para no calcular dos
veces, el plan de cada aplicación se construye una sola vez y de él salen el score, el
resumen del roadmap y el dashboard.

## Corrección encontrada en el camino

Al pasar a parámetros el índice de riesgo de Insights apareció un error: contaba como
«gestión de vulnerabilidades débil» a las aplicaciones con **Kenna menor que 50**. Kenna va
de 0 a 1000 y **más alto es peor** (el BRD puntúa 0–339 con 100 y 670–1000 con 20): el
criterio estaba invertido y sumaba las aplicaciones con el mejor riesgo de host. En los
datos dummy eran 21 aplicaciones contadas de más y 40 de menos. Ahora es **Kenna ≥ 670**, el
inicio de la banda Critical/High, y el umbral es un parámetro. La prueba de Insights tenía
el mismo supuesto al revés y se corrigió.

## Consecuencias

- **D3 del plan está hecha.** Cambiar un umbral desde el portal mueve el dashboard sin
  desplegar, y cada indicador tiene sus seis campos en *Definitions*.
- **El administrador de reglas administra también las definiciones.** Es el mismo rol y la
  misma reautenticación. Si el negocio quiere separar quién cambia el score de quién cambia
  las definiciones, hace falta un rol nuevo (queda como pregunta).
- **Una versión de reglas puede cambiar solo definiciones.** Aparece en la línea de tiempo y
  en el aviso de reglas nuevas como cualquier otra, con sus cambios nombrados.
- **Las versiones anteriores al cambio muestran las definiciones de fábrica de hoy**, no las
  que había cuando se publicaron: entonces no existían como parte de la versión.
- **Riesgo asumido:** interpretar «el score que viene desde CMT» como `Raw_Tech_Score`. Si el
  negocio se refería a otro campo, cambia la fuente de un extractor, no la definición.
