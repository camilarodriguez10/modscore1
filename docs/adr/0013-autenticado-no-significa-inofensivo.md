# ADR 0013 — Autenticado no significa inofensivo

**Estado:** aceptada · **Fecha:** 2026-09-11

## Contexto

Hasta la Fase 07 los controles de recursos suponían que el riesgo estaba fuera: la
autenticación era la barrera, y del otro lado todos se portaban bien. La revisión
encontró que un usuario con sesión —o una cuenta comprometida— podía degradar el
servicio para todos:

- **El rate limiter corría antes de autenticar.** Nadie tenía balde propio: todos
  compartían el de su IP, y detrás del ingress esa IP es la misma para todos.
  Agotarlo dejaba sin servicio a la organización entera.
- **La historia releía y recalculaba todas las versiones en cada consulta.** Con 30
  cargas, un segundo por petición medido; crecía con cada carga.
- **La carga pasaba por el cliente HTTP estándar,** con 10 s por intento y
  reintentos: un archivo grande fallaba, y un reintento tras un timeout podía
  publicar dos versiones.
- **Kestrel aceptaba 30 MB en cualquier endpoint** JSON, y nada impedía dos cargas
  de 256 MB en paralelo.

## Decisión

| Control | Valor | Por qué ese valor |
|---|---|---|
| Orden del pipeline | autenticación → rate limit → autorización | El límite es por usuario |
| Partición | `usuario:<subject>`; sin sesión, `ip:<dirección>` | Un usuario no consume el balde de otro |
| IP real | `X-Forwarded-For` solo de proxies declarados | Un cliente no puede inventarse la IP |
| Límite general | 120 peticiones por minuto | Una persona navegando no se acerca |
| Simulaciones | 30 por minuto | Cada una recalcula el score varias veces |
| Cargas | 5 por 10 minutos, ventana deslizante | Mueven cientos de MB y reemplazan el portafolio |
| Cuerpo por defecto | 1 MB | Ningún JSON del sistema necesita más |
| Cuerpo de la carga | 256 MB, ampliado solo en su endpoint | El tope documentado desde la Fase 07 |
| Campo de CSV | 65 536 caracteres (4 096 hasta el 2026-10-05) | Las columnas que se cargan son cortas, pero el export real de Veracode trajo una celda de más de 4 096, en texto libre que no se usa ([ADR 0030](0030-una-carga-grande-cabe-en-el-pod.md)) |
| Publicación | una a la vez; la segunda se rechaza con motivo | No hay nada que ganar en paralelo |
| Historia | calculada una vez por versión | Una versión publicada es inmutable |
| Cliente de carga | sin reintentos, 5 minutos | Reintentar publicaría dos versiones |
| POST en general | nunca se reintentan | La regla no puede depender de recordar cuál es idempotente |

Todos los límites de frecuencia son configurables (`RateLimiting:*`). Un rechazo
responde 429 con `Retry-After` y deja un evento de auditoría (4103).

## Consecuencias

- **Los números son de partida, no dogma.** Dependen del tamaño real del equipo; la
  prueba de carga (`tools/Modernization.LoadTest`) da la base para ajustarlos.
- **Un script legítimo necesita su propia identidad.** Una integración que consulte
  en volumen tiene que autenticarse como tal, no con la sesión de una persona.
- **La carga es la operación más restringida del sistema:** rol propio, alcance
  completo, frecuencia baja, tamaño acotado, una a la vez. Es coherente con lo que
  hace: reemplazar la base de las decisiones de inversión.
