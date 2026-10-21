# ADR 0035 — Navegación sin Insights, y Code y Roadmap apagables desde la capa Channel

**Estado:** aceptada · **Fecha:** 2026-10-21 · **Relacionado:** [ADR 0023](0023-las-seis-preguntas-se-responden-con-lo-que-ya-hay.md), [ADR 0025](0025-las-definiciones-viajan-con-las-reglas.md), [ADR 0026](0026-el-analisis-de-codigo-llega-de-github-enterprise.md), [ADR 0029](0029-el-sitio-compilado-viaja-con-el-codigo.md)

## Contexto

El 2026-10-21 se pidió quitar la pestaña **Insights**, porque responde lo mismo que el
dashboard, y ocultar **Code** y **Roadmap**. En el equipo de la empresa el sitio llega ya
compilado y no se puede volver a compilar (ADR 0029): una pantalla que se oculta en el código
de la SPA solo vuelve con un sitio nuevo.

## Decisión

1. **Insights se retira entero.** Se van la pestaña, la pantalla, `GET /api/insights` y
   `GET /system/insights`, el analizador (`InsightsAnalyzer`), sus doce avisos y sus trece
   parámetros en *Rules → Portfolio analytics*. Lo que no se usa no queda expuesto. Una
   versión de reglas publicada antes trae la sección `insights` en sus definiciones: se
   ignora al leerla, y la versión sigue cargando. Un enlace guardado a `/insights` abre el
   dashboard con su filtro.
2. **Code y Roadmap se apagan desde la configuración de la capa Channel.** La sección
   `Features` tiene dos interruptores: `Code` y `PortfolioRoadmap`. Los dos vienen apagados en
   `appsettings.json`. Una pantalla apagada:
   - no se ofrece: la sesión (`GET /api/session`) dice qué pantallas están encendidas, y la
     SPA arma la navegación con eso;
   - no se abre: su ruta vuelve al dashboard;
   - no responde: sus endpoints (`/api/code`, `/api/code/applications/{eai}`, `/api/roadmap`,
     `/api/roadmap/export.csv`) devuelven 404;
   - no pide: con Code apagada, *Definitions* y *Rules* no muestran sus parámetros.

   Se encienden con `Features__Code=true` y `Features__PortfolioRoadmap=true`, sin recompilar
   el sitio. El chequeo es por petición.
3. **El plan de cada aplicación no es la pantalla Roadmap.** `/applications/{eai}/roadmap` y su
   endpoint siguen siempre disponibles: el *future plan* se abre desde la aplicación.

## Alternativas consideradas

- **Ocultar las pestañas en el código de la SPA:** volver a mostrarlas exigiría compilar el
  sitio, lo que el equipo de la empresa no puede hacer.
- **Ocultar Insights como Code y Roadmap:** dejaría un endpoint y trece parámetros de una
  pantalla que no se va a usar, sin que nadie los mantenga.

## Consecuencias

- La navegación queda en Dashboard, Portfolio, Data, Definitions y Rules (para quien
  administra reglas).
- Las pruebas de la API y de punta a punta encienden Code y Roadmap para seguir probándolas.
  Hay pruebas aparte para el valor por defecto: la sesión las da apagadas y sus endpoints
  responden 404.
- Las preguntas que respondía Insights siguen en el dashboard. Lo que solo estaba en Insights
  deja de existir: el peso de negocio, el índice de riesgo por grupo y el umbral de
  concentración elegible desde la URL.
