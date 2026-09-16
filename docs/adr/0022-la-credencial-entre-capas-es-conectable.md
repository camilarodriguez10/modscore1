# ADR 0022 — La credencial entre capas es conectable, y APIM emite la de verdad

**Estado:** aceptada · **Fecha:** 2026-09-16 · **Sigue a:** [ADR 0020](0020-camino-de-oidc-a-oauth-con-apim.md) · **Relacionado:** H-16, PA-02, PA-05, SEC-03, ASVS 2.10.1

## Contexto

El ADR 0020 dejó tres opciones para entrar a APIM y recomendó la **C junto con la A**.
El 2026-09-16 se eligió esa:

- **A.** APIM delante del BFF. La capa Channel sigue con su sesión del lado servidor y su
  cookie; APIM valida, limita y observa en el borde.
- **C.** APIM también entre las dos capas: el token de servicio estático —el hallazgo
  **H-16**— se reemplaza por uno de vida corta.

APIM no existe todavía. Esta fase deja el código listo y probado con dobles; lo que falta
es el emisor real.

## Decisión

**La credencial con que la capa Channel llama a la System vive detrás de una interfaz, y
la capa System valida lo que recibe con un validador también conectable.**

| Pieza | Hoy | Con APIM |
|---|---|---|
| Qué manda la Channel | `StaticServiceCredential`: el secreto del gestor de secretos | `ClientCredentialsServiceCredential`: token de vida corta pedido con `client_credentials`, cacheado y renovado un minuto antes de vencer |
| Qué valida la System | `StaticServiceTokenValidator`: comparación en tiempo constante, con token de rotación | `JwtServiceTokenValidator`: firma, emisor, audiencia, vencimiento, algoritmo de una lista blanca y, si se configura, lista de clientes |
| Cómo se elige | `ServiceToken:Mode` y `SystemApi:Credential:Mode` | Igual, cambiando el valor |

Tres decisiones dentro de esa:

1. **Un modo `Both` para la ventana del cambio.** La capa System puede aceptar el secreto
   compartido y el token de APIM a la vez. Sin eso, cambiar las dos capas a la vez exigiría
   un corte, y un corte invita a hacerlo mal.
2. **La credencial se pide por petición, no al arrancar.** Un token de vida corta cambia;
   una cabecera fijada en el cliente HTTP se queda vieja. Lo pone un
   `DelegatingHandler`, que además cubre el cliente de cargas: el primer intento lo olvidó
   y lo encontraron las pruebas.
3. **El token nunca se registra.** Ni entero ni en parte, ni siquiera cuando el emisor
   responde con error: el cuerpo del error tampoco se repite en el mensaje.

## Lo que no cambia

La sesión del lado servidor, la cookie `__Host-` con `SameSite=Strict`, el doble envío
CSRF, la ventana de reautenticación de 10 minutos, los permisos y el alcance por
LOB/CIO/país. APIM se suma al borde; no reemplaza al BFF (ADR 0011).

## Reautenticación y alcance

- **Reautenticación (opción A).** `ApimReauthentication` cierra la sesión anterior y sale
  al mismo flujo de código de autorización, con los parámetros que APIM espera —por
  ejemplo `acr_values=mfa`— tomados de `Authentication:Apim:AuthorizeParameters`. Exigir
  MFA sigue siendo política del IdP o de APIM, no del código (SEC-04).
- **Alcance (PA-03).** Si el IdP no puede emitir el claim `portfolio_scope`, se mapean
  grupos: `Authorization:ScopeFromGroups`. **El claim manda sobre los grupos**; sumarlos
  podría ampliar un alcance ya emitido, y ampliar el alcance por accidente es justo lo que
  no puede pasar (H-02).

## Consecuencias

- H-16 se cierra el día que `ServiceToken:Mode` quede en `Jwt` y el secreto compartido
  salga del gestor de secretos. Hasta entonces sigue como parcial, con el código listo.
- La capa System no habla con el IdP para las personas: solo descubre las llaves del
  emisor de los tokens de servicio.
- Se agregan dos paquetes de Microsoft que ya estaban en el árbol por OIDC
  (`Microsoft.IdentityModel.JsonWebTokens` y `...Protocols.OpenIdConnect`), con la misma
  versión que ya usaba la solución.
- El cambio, paso a paso, está en el [runbook de paso a APIM](../runbooks/paso-a-apim.md).

## Qué queda por verificar contra APIM real

1. Que el token que emite APIM traiga la audiencia de la capa System y un `azp` estable.
2. Que la reautenticación con `max_age=0` o `acr_values` llegue efectivamente al IdP
   (SEC-19).
3. Que el claim de alcance —o el mapeo de grupos— entregue lo que el negocio espera.
