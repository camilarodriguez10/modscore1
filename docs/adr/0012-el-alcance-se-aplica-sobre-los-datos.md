# ADR 0012 — El alcance del portafolio se aplica sobre los datos

**Estado:** aceptada · **Fecha:** 2026-09-11 · **Pendiente:** RFC-10 (modelo de claims del IdP)

## Contexto

El plan (secc. 7.2) prometía autorización por LOB y CIO **filtrada en la consulta,
no en la interfaz**. Al llegar a la Fase 08 no existía: cualquier usuario
autenticado veía el portafolio completo, incluidas las listas de CVEs y librerías
vulnerables por aplicación. Para alguien con malas intenciones, eso es un mapa de
qué atacar primero.

Tampoco había roles: **publicar una carga —reemplazar el portafolio de toda la
organización— estaba al alcance de cualquier sesión.**

## Decisión

Dos ejes independientes, derivados de los claims del usuario:

| Eje | Claim | Valores | Qué decide |
|---|---|---|---|
| Rol | `roles` | `portfolio.read`, `ingestion.write` | Qué operaciones puede hacer |
| Alcance | `portfolio_scope` (configurable) | `*`, `lob:…`, `country:…`, `cio:…` | Qué aplicaciones ve |

**Cómo viaja el alcance — subsistema de confianza.** La capa Channel lo calcula
desde la sesión y lo adjunta a cada llamada a la System en `X-Portfolio-Scope`,
junto con el subject seudónimo en `X-Portfolio-User`. El `DelegatingHandler` borra
esos headers antes de escribirlos: un valor enviado por el navegador nunca llega.
La capa System solo los acepta de un llamador autenticado con token de servicio;
de cualquier otro, el alcance es vacío.

**Dónde se aplica.** En `PortfolioService`, que es el único camino a los datos.
Toda lectura exige un `PortfolioScope` y **no existe sobrecarga sin él**: olvidar
el alcance en un endpoint nuevo no compila.

**Reglas:**

- **Por defecto no se ve nada.** Sin claim, sin header o con entradas mal formadas,
  el alcance es vacío. Nunca se interpreta como el portafolio completo.
- **El alcance es la unión** de sus entradas.
- **Fuera de alcance es 404**, igual que inexistente: la respuesta no confirma que
  la aplicación exista.
- **Linaje y carga exigen alcance completo.** El linaje tiene totales de archivos
  enteros y los EAI de las filas rechazadas: no se puede recortar sin mentir en
  los totales. Una carga reemplaza todo el portafolio, no un tramo.
- **La historia se evalúa por versión**, con los atributos que la aplicación tenía
  en cada carga.
- **Los agregados siguen al alcance:** quien ve un LOB ve el promedio de su LOB.

## Consecuencias

- **La integridad del alcance depende del token de servicio y de mTLS.** Quien
  pueda llamar a la capa System como servicio puede declarar cualquier alcance.
  Es la misma frontera que ya protegía los datos; no se agrega una nueva, pero se
  documenta explícitamente.
- **El IdP tiene que emitir el claim (RFC-10).** El nombre es configurable
  (`Authorization:ScopeClaimType`). Hasta confirmarlo con el equipo de identidad,
  un usuario real sin claim no ve nada, y la pantalla lo dice.
- **La lista de versiones muestra el total de aplicaciones de cada carga.** Es
  metadato de la carga, no del portafolio de nadie; se acepta.
- **La interfaz no ofrece lo que se va a negar:** la sesión devuelve los permisos
  resueltos, la pestaña de linaje desaparece sin alcance completo y el formulario
  de carga explica qué rol falta. La decisión real sigue en el servidor.
