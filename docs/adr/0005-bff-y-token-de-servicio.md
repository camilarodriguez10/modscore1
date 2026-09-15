# ADR 0005 — BFF con cookie de sesion y token de servicio

**Estado:** aceptada · **Fecha:** 2026-09-10

## Contexto

Una SPA que guarda el token de acceso en `localStorage` lo pierde entero ante
cualquier XSS. Y la capa System expone el portafolio completo: no puede quedar
alcanzable con solo una sesion de usuario.

## Decision

**Capa Channel.** Patron BFF. El flujo OIDC (Authorization Code + PKCE) termina
en el servidor; los tokens se guardan del lado servidor y el navegador recibe una
cookie `__Host-` httpOnly, `Secure`, `SameSite=Strict`. El API responde 401 y 403;
nunca redirige a una pagina de login.

**Capa System.** Token de servicio en `Authorization: Bearer`, comparado en tiempo
constante. Es el control de aplicacion; mTLS entre pods es el de transporte y lo
impone el service mesh. Uno no sustituye al otro.

**Esquema de desarrollo.** Existe para trabajar sin un proveedor OIDC levantado.
No autentica a nadie, asi que solo se activa cuando el entorno es Development *y*
la configuracion lo pide; si esas dos condiciones no se cumplen juntas, el proceso
no arranca. Firmar la sesion es un acto explicito del endpoint de login, nunca el
efecto de fallar la autorizacion en otro endpoint.

## Consecuencias

- Un XSS en la SPA no puede robar el token: no esta en el navegador.
- El token CSRF se emite en hexadecimal, no en Base64. ASP.NET Core codifica en
  URL el valor de la cookie al escribirla, y un valor con `+`, `/` o `=` no
  coincidiria con lo que el cliente lee de `document.cookie` y reenvia en el
  header. Se detecto con un test antes de que llegara al frontend.
- Los mensajes de error no hacen eco de la entrada del usuario. Un EAI code se
  valida por lista blanca en las dos capas y se rechaza con 400 si no es una
  secuencia de hasta 10 digitos.
- El token de servicio vive en Key Vault via CSI driver. El valor de desarrollo
  esta en `appsettings.Development.json` y no sirve en ningun otro entorno.
