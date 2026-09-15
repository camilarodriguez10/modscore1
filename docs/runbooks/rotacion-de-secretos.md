# Runbook — Rotación de secretos

**Cuándo:** calendario trimestral, salida de una persona con acceso, o sospecha de
exposición. En este último caso, seguir primero el
[runbook de incidente](incidente-de-seguridad.md).

**Dónde viven:** en el gestor de secretos, montados como archivos por el CSI driver
en `/mnt/secrets` (`Secrets:Directory`). Nunca en variables de entorno ni en el
repositorio: `DeploymentGuards` impide arrancar fuera de Development con el valor
de desarrollo.

| Secreto | Archivo | Lo usa |
|---|---|---|
| Token de servicio vigente | `ServiceToken__Token` (System) · `SystemApi__ServiceToken` (Channel) | Autenticación Channel → System |
| Token de servicio entrante | `ServiceToken__SecondaryToken` (System) | Solo durante la rotación |
| Secreto del cliente OIDC | `Authentication__Oidc__ClientSecret` (Channel) | Canje del código con el IdP |
| Llaves de Data Protection | volumen `/keys` (Channel) | Cifrado de cookie y ticket de sesión |

---

## 1. Token de servicio — sin corte

La capa System acepta dos tokens a la vez precisamente para esto.

1. **Generar** el token nuevo: al menos 32 caracteres aleatorios.
   ```bash
   openssl rand -base64 48
   ```
2. **System acepta los dos.** Publicar el nuevo como `ServiceToken__SecondaryToken`
   y reiniciar system-api. Verificar que arrancó: `GET /health` → 200.
3. **Channel usa el nuevo.** Publicar el nuevo como `SystemApi__ServiceToken` y
   reiniciar channel-api. Verificar una pantalla con datos.
4. **System olvida el anterior.** Mover el nuevo a `ServiceToken__Token`, dejar
   vacío `ServiceToken__SecondaryToken`, reiniciar system-api.
5. **Confirmar** que el token anterior ya no sirve:
   ```bash
   curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer <anterior>" http://system-api:8080/system/ruleset
   ```
   Esperado: `401`.

**Si algo falla en el paso 3**, channel-api sigue con el token anterior, que la
System todavía acepta: no hay corte. Volver a publicar el anterior y revisar.

## 2. Secreto del cliente OIDC

1. Crear un secreto nuevo en el IdP **sin revocar el actual**.
2. Publicarlo como `Authentication__Oidc__ClientSecret` y reiniciar channel-api.
3. Iniciar sesión con un usuario de prueba.
4. Revocar el secreto anterior en el IdP.

Las sesiones abiertas no se ven afectadas: el secreto solo se usa al iniciar sesión.

## 3. Llaves de Data Protection

ASP.NET Core rota las llaves solo cada 90 días y conserva las anteriores para
descifrar lo ya emitido. **No hace falta rotarlas a mano.**

**Revocarlas sí es una acción de emergencia:** invalida de golpe todas las sesiones
y obliga a todos a iniciar sesión de nuevo. Se usa cuando se sospecha que las llaves
o el almacén de sesiones quedaron expuestos.

1. Detener channel-api.
2. Mover el contenido de `/keys` a un lugar de evidencia (no borrarlo: sirve para el
   análisis posterior).
3. Arrancar channel-api: genera llaves nuevas.
4. Verificar que una sesión anterior responde `401` en `/api/session`.

**Respaldo:** el volumen `/keys` entra en el respaldo diario
([recuperación ante desastres](recuperacion-ante-desastres.md)). Perderlo equivale a
revocarlas.
