# ADR 0011 — La sesión vive en el servidor

**Estado:** aceptada · **Fecha:** 2026-09-11

## Contexto

Hasta la Fase 07 la cookie cifrada **era** la sesión. Eso tenía tres consecuencias
que la revisión de la Fase 08 clasificó como hallazgos:

- **Cerrar sesión no la cerraba.** `SignOutAsync` borraba la cookie del navegador,
  pero una copia seguía siendo válida hasta expirar. Quien la hubiera obtenido
  —un proxy mal configurado, un volcado de memoria, un XSS en otra aplicación del
  mismo dominio— conservaba el acceso ocho horas.
- **No había tope absoluto.** Con expiración deslizante de 8 h, una sesión en uso
  continuo no vencía nunca. ASVS 3.3.2 (nivel 2) pide reautenticación tras 30
  minutos de inactividad y, aun con uso, tras 12 horas.
- **La cookie guardaba los tokens del IdP** (`SaveTokens = true`), aunque la capa
  Channel nunca llama APIs en nombre del usuario.

## Decisión

- **`ITicketStore` sobre `IDistributedCache`.** La cookie lleva una clave aleatoria
  de 256 bits; el ticket vive en el servidor, cifrado con Data Protection antes de
  guardarse. En un nodo, el cache es de memoria; con varias réplicas se registra
  uno compartido (Redis) sin tocar el resto.
- **Inactividad: 30 minutos.** Expiración deslizante.
- **Absoluto: 12 horas desde el inicio de sesión.** La marca de inicio va en las
  propiedades del ticket y sobrevive a las renovaciones; `OnValidatePrincipal` la
  compara con el reloj en cada petición.
- **Cerrar sesión elimina el ticket.** Una copia de la cookie deja de valer en el
  mismo instante.
- **Sin fijación de sesión.** Con OIDC, el callback del IdP es una petición
  cross-site y la cookie `SameSite=Strict` no viaja en ella: el inicio de sesión
  siempre crea una clave nueva, y una cookie plantada no sirve. En desarrollo,
  volver a iniciar sesión con una abierta no firma otra encima, porque el manejador
  de cookies renovaría la misma clave en lugar de emitir una nueva.
- **`SaveTokens = false`.** No se retiene lo que no se usa.

Los dos tiempos son configurables (`Session:IdleTimeoutMinutes`,
`Session:AbsoluteTimeoutHours`), y el reloj es un `TimeProvider` inyectado: las
pruebas verifican ambos vencimientos moviendo el reloj, no esperando.

## Consecuencias

- **Una réplica del channel-api hasta tener cache compartido (RFC-11).** Con cache
  de memoria por pod, una segunda réplica no conoce las sesiones de la primera.
  El manifiesto lo fija en `replicas: 1` y lo explica.
- **Reiniciar el pod cierra las sesiones.** Con cache de memoria es el precio; con
  Redis desaparece.
- **Las llaves de Data Protection tienen que persistir** (`DataProtection:KeysDirectory`).
  Revocarlas invalida todas las sesiones de una vez, y eso es una herramienta de
  respuesta a incidentes (runbook de incidente de seguridad).
- **`SameSite=Strict` es parte del control de fijación.** Relajarla a `Lax` —por
  ejemplo, para un flujo de terceros— obliga a revisar esta decisión: con `Lax`, un
  callback por GET llevaría la cookie previa.
- **Verificado por pruebas:** copia de cookie inválida tras logout, sesiones
  independientes entre inicios de sesión, reinicio de sesión sin firma encima,
  vencimiento a los 31 minutos sin actividad, y vencimiento al cruzar las 12 horas
  con actividad cada 20 minutos.
