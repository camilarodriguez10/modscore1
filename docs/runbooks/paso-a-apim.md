# Runbook — Paso a APIM sin corte

**Cuándo:** cuando exista el producto de APIM y el emisor de tokens. Decisión y diseño en el
[ADR 0022](../adr/0022-la-credencial-entre-capas-es-conectable.md); opciones en el
[ADR 0020](../adr/0020-camino-de-oidc-a-oauth-con-apim.md).

**Qué cambia:** la credencial entre las dos capas deja de ser un secreto compartido y pasa a
ser un token de vida corta (opción C), y APIM queda delante del BFF (opción A).

**Qué no cambia:** la sesión del lado servidor, la cookie, el CSRF, los permisos, el alcance
y la ventana de reautenticación de 10 minutos.

---

## Antes de empezar

| Qué se necesita | De quién |
|---|---|
| Producto de APIM con su política, y la URL del emisor de tokens | Plataforma |
| Registro de aplicación para la capa Channel: `client_id` y secreto | Identidad |
| Audiencia —o `scope`— que identifique a la capa System | Identidad |
| Confirmación de qué claim trae el alcance, o el mapa de grupos (PA-03) | Identidad + negocio |

El secreto del cliente llega como **archivo del gestor de secretos**, igual que los demás
(`Secrets:Directory`). Nunca como variable de entorno.

## Orden del cambio

El orden importa: la capa System aprende a aceptar el token **antes** de que la Channel
empiece a mandarlo, y el secreto viejo se retira **después**.

### 1. La capa System acepta los dos

```bash
ServiceToken__Mode=Both
ServiceToken__Jwt__Authority=https://login.example.com/tenant/v2.0
ServiceToken__Jwt__Issuer=https://login.example.com/tenant/v2.0
ServiceToken__Jwt__Audience=api://modernization-system
ServiceToken__Jwt__AllowedCallers__0=<client_id de la capa Channel>
```

- El secreto compartido sigue configurado y sigue funcionando.
- **Comprobar:** la aplicación responde igual que antes; nada cambia para quien la usa.

### 2. La capa Channel pide el token

```bash
SystemApi__Credential__Mode=ClientCredentials
SystemApi__Credential__TokenEndpoint=https://login.example.com/tenant/oauth2/v2.0/token
SystemApi__Credential__ClientId=<client_id>
SystemApi__Credential__Scope=api://modernization-system/.default
```

El secreto va en el archivo `SystemApi__Credential__ClientSecret` del volumen de secretos.

- **Comprobar:** el portafolio carga; en el log de la capa System no aparece el evento 5304
  («token rechazado por la validación JWT»). Un 5304 repetido significa que la audiencia, el
  emisor o las llaves no son los que se configuraron.

### 3. Se retira el secreto compartido

Una vez que **todas** las réplicas de la capa Channel están en el paso 2:

```bash
ServiceToken__Mode=Jwt
```

- Borrar `ServiceToken__Token` y `ServiceToken__SecondaryToken` del gestor de secretos.
- **Esto es lo que cierra H-16.** Anotarlo en [hallazgos.md](../security/hallazgos.md) y pasar
  ASVS 2.10.1 a cumple en [asvs-l2.md](../security/asvs-l2.md).

### 4. APIM delante del BFF

```bash
Authentication__Provider=Apim
Authentication__Oidc__Authority=<autoridad que expone APIM>
Authentication__Apim__AuthorizeParameters__acr_values=mfa
```

- **Comprobar:** publicar una versión de reglas vuelve a pedir el segundo factor, y el
  evento 4004 queda registrado (SEC-04, SEC-19).

## Si algo sale mal

| Síntoma | Qué mirar | Vuelta atrás |
|---|---|---|
| La capa System responde 401 a todo | Evento 5304: emisor, audiencia o llaves | `SystemApi__Credential__Mode=Static` en la Channel |
| Errores al pedir el token | El emisor responde 400 o 401; el mensaje trae el código, no el cuerpo | Ídem |
| La reautenticación no pide credenciales | El IdP ignora `max_age=0`; revisar `acr_values` | `Authentication__Provider=Oidc` |
| Alcance vacío para todos | El claim cambió de nombre, o falta el mapa de grupos | `Authorization__ScopeClaimType`, o `Authorization__ScopeFromGroups` |

La vuelta atrás siempre es configuración: no hay que desplegar otra versión. Mientras
`ServiceToken__Mode` esté en `Both`, las dos credenciales funcionan.

## Después del cambio

- Cerrar H-16 y SEC-03 en [PENDING-ITEMS.md](../PENDING-ITEMS.md).
- Verificar SEC-19 contra el proveedor real y anotarlo en
  [verificación ASVS](verificacion-asvs.md).
- Revisar que las alertas del SIEM incluyan 5304 y 5305 (token rechazado y cliente
  inesperado).
