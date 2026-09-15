# ADR 0016 — El filtro vive en la URL y solo acota

**Estado:** aceptada · **Fecha:** 2026-09-12 · **Fase:** 10

## Contexto

Los filtros vivían en el estado local de la pantalla de portafolio: se perdían al
cambiar de menú, no se podían compartir y las demás pantallas no los conocían. Los
puntos 1, 2 y 4 de la ampliación piden lo contrario: filtros en todas las vistas que
sobrevivan a la navegación, estadísticas clicables y un roadmap agrupable por
cualquier dimensión.

Hacer clicable una estadística obliga a algo menos visible: **la clave que muestra un
agregado tiene que ser exactamente el valor que entiende el filtro**. Si no, el clic
devuelve un conjunto vacío. La primera versión de esta fase lo tenía roto para los
roadmaps: el inventario emitía `Maintain (Patch)` y el filtro esperaba `MaintainPatch`.

## Decisión

**La URL es la fuente de verdad del filtro.** `?country=Mexico&lob=Individual` viaja
en cada enlace del menú, sobrevive a un refresco y se puede pegar en un chat.

La sesión del navegador recuerda el último filtro, con tres reglas:

- **Un enlace que llega sin el filtro lo recupera.** La pestaña de un detalle o una
  redirección no tienen por qué conocerlo, y no por eso se pierde.
- **Quitarlo desde la barra borra la memoria en el mismo acto**, así que restituir
  nunca resucita lo que el usuario acaba de quitar.
- **El botón atrás sí lo quita.** Volver a una URL sin filtro es una decisión del
  usuario, no un enlace que lo olvidó.

**Un solo conjunto de filtros para todas las pantallas de consulta:** LOB, país, CIO,
disposition, roadmap, tramo de score y criticidad. La barra vive en el armazón y se
muestra con controles donde el filtro actúa (portafolio y roadmap); en el detalle de
una aplicación y en Datos se muestra solo el filtro activo, con el enlace de vuelta.

**Un solo vocabulario.** Los agregados emiten el nombre de pantalla y el filtro lo
acepta. Para disposition y roadmap el filtro acepta también el nombre del enum, para
quien llame al API directamente, y los dos devuelven lo mismo.

**El tramo pasa al dominio.** Filtrar por tramo exige puntuar, y solo la capa System
tiene los scores. Con la tabla de tramos duplicada en las dos capas, la página y el
agregado podrían discrepar sobre qué es "En riesgo".

**El filtro se aplica en la capa System, sobre el conjunto ya recortado por el
alcance** ([ADR 0012](0012-el-alcance-se-aplica-sobre-los-datos.md)). No existe
combinación de parámetros que devuelva algo que el alcance no permita, porque el
filtro nunca ve el portafolio original.

**Las opciones de la barra salen del portafolio sin filtrar**, dentro del alcance. Si
se recalcularan con el filtro puesto, elegir Chile dejaría la lista de países con un
solo país y no habría cómo volver.

## Consecuencias

- **Un enlace compartido nunca muestra más de lo que el receptor puede ver.** Si
  quien lo abre tiene un alcance más estrecho, ve menos; el enlace lleva criterios,
  no datos.
- **Una entrada inválida es un 400, no un conjunto vacío.** Un tramo o una agrupación
  desconocidos se rechazan con las opciones válidas: cero resultados haría pensar que
  el portafolio está vacío.
- **La prueba que protege esto recorre todas las claves.** Por cada dimensión, cada
  clave que emite un agregado se usa como filtro y debe devolver exactamente su
  conteo; lo mismo con cada grupo del roadmap. Es la prueba que encontró el desajuste
  de los roadmaps.
- **Los valores de filtro quedan en la URL**, y por lo tanto en el historial del
  navegador y en los logs de acceso. Son nombres de dimensiones de la organización
  —países, LOB, CIO—, no datos personales ni del portafolio; se acepta.
- **La primera versión restituía el filtro solo al cargar la página**, y el enlace a
  Datos no lo llevaba: pasar por Datos y volver dejaba el portafolio sin filtro. Lo
  encontró la verificación en vivo, no las pruebas de API, porque es un
  comportamiento de navegación; de ahí las tres reglas de arriba.
- **Agrupar no recalcula.** El roadmap por país promedia las mismas proyecciones que
  el plan individual de cada aplicación; una prueba lo compara aplicación por
  aplicación.
