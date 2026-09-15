# ADR 0006 — SPA sin librería de gráficos

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

El plan contemplaba Recharts para el waterfall, el gauge y la distribución. Las
tres visualizaciones que la aplicación necesita son, en realidad:

- **el score**: una cifra guía con su posición en la escala 20–100 — un titular,
  no un gráfico;
- **la composición del numerador**: una barra apilada de anchos proporcionales;
- **la distribución y los agregados**: filas con barra y etiqueta directa.

Ninguna necesita ejes, escalas continuas ni interpolación.

## Decisión

Se dibujan con HTML y CSS propios en lugar de traer una librería de gráficos.

La paleta de datos se validó con el validador de la guía de visualización contra
las superficies reales de la aplicación (claro `#ffffff`, oscuro `#141d22`):

| Paleta | Resultado |
|---|---|
| Categórica, 3 slots (pilares) | PASS en los cinco gates, ambos modos, lista de pares completa |
| Estado, 4 tramos | Fija, nunca tematizada; en modo claro *warning* y *serious* quedan bajo 3:1 a propósito |

## Consecuencias

- Una dependencia menos en el árbol, coherente con el criterio de adopción del
  plan y con `npm audit` en cero.
- El control sobre el detalle es total: los huecos de 2 px entre segmentos, el
  tramo "sin ganar" al final de la barra y la escala que arranca en 20 son
  decisiones de diseño que una librería genérica no habría permitido sin pelear.
- **El color nunca es el único canal.** Los tres pilares llevan leyenda siempre
  presente y la tabla "Aporte por métrica" repite la misma información sin
  depender del color. Cada chip de tramo lleva su nombre escrito.
- **El texto lleva tokens de texto, no el color de la serie.** La cifra de 79,67
  se dibuja en tinta primaria y el chip de al lado carga el color de estado: una
  cifra de 64 px en amarillo *warning* sobre blanco sería el peor lugar posible
  para gastar un color de bajo contraste.
- Si más adelante aparece una serie temporal — la evolución del score por
  trimestre — esa sí pide una librería. La decisión se revisa entonces, no antes.
