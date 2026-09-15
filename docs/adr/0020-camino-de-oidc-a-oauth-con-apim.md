# ADR 0020 — Camino de OIDC a OAuth mediante APIM

**Estado:** aceptada · **Fecha:** 2026-09-14 · **Relacionado:** ADR 0005, ADR 0011, ADR 0013, RFC-12

## Contexto

La capa Channel autentica hoy con OIDC (Authorization Code + PKCE) contra el IdP
corporativo y guarda la sesion del lado servidor (patron BFF). La organizacion va a
centralizar la autenticacion de APIs en **Azure API Management (APIM)** con OAuth.
Ese cambio llegara despues del piloto y no tiene fecha.

Invertir ahora en detalles propios de OIDC (sesiones del IdP, logout de canal
trasero, afinamiento de `max_age`) seria trabajo que se tira. Pero la Fase 12 dejo
dos puntos acoplados al proveedor: la reautenticacion antes de publicar reglas y la
lectura de `auth_time`.

## Decision

1. **Todo lo especifico del proveedor queda detras de interfaces o en un solo tipo.**
   - `IReauthenticationFlow` inicia la reautenticacion. Hay una implementacion de
     desarrollo y una OIDC; APIM agrega la suya.
   - `StepUp` es el unico que lee la marca de autenticacion del token (`auth_time`) y
     el unico que escribe el claim propio `modscore_reauth_at`. El resto del sistema
     solo lee ese claim.
   - `ChannelAuthentication` es el unico que registra esquemas.
2. **Lo que no cambia con APIM:** la sesion del lado servidor y su almacen, la cookie
   `__Host-` SameSite=Strict, CSRF doble envio, la ventana de reautenticacion de 10
   minutos, los permisos (`ChannelPermissions`), el alcance por LOB/CIO/pais y los
   encabezados reenviados a la capa System con el token de servicio.
3. **Toda reautenticacion cierra la sesion anterior** antes de abrir la nueva, sea
   cual sea el proveedor. Con OIDC la vuelta desde el IdP no trae la cookie
   SameSite=Strict y no se podia reemplazar al volver.

## Opciones para APIM, a decidir con plataforma

| Opcion | Como queda | Que cambia aqui |
|---|---|---|
| A. APIM delante del BFF, OAuth hacia el IdP | El BFF sigue con sesion propia; APIM valida y limita | Nada estructural. `IReauthenticationFlow` para el flujo de APIM |
| B. APIM valida un JWT que emite el IdP y lo pasa al BFF | El BFF deja de hacer el Authorization Code | Nuevo esquema JWT Bearer entre APIM y Channel. La SPA sigue sin tokens: la cookie la emite el BFF tras validar |
| C. APIM entre Channel y System | Reemplaza el token de servicio estatico | Cierra H-16 y RFC-12; `ServiceTokenAuthenticationHandler` pasa a validar el token de APIM |

La opcion C es compatible con A o B y es la que mas seguridad agrega.

## Consecuencias

- La prueba `ReauthenticationFlowTests` fija el contrato: cerrar la sesion antes del
  challenge, marcar el challenge como reautenticacion y volver solo a rutas de la
  lista blanca.
- Mientras tanto la verificacion contra un IdP real de `max_age=0` y `auth_time`
  sigue pendiente (plan 8.5). Con APIM se verifica el flujo que la reemplace.
- Los pendientes de la migracion estan en `docs/PENDING-ITEMS.md` (seccion
  *Authentication via APIM*).
