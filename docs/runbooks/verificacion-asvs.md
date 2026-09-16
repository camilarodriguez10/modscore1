# Runbook — Verificación de los parciales de ASVS

**Cuándo:** cuando exista un entorno de prueba con cluster, IdP, ingress y gestor de
secretos reales. Hasta entonces, los once requisitos de abajo quedan como **parcial** en
[asvs-l2.md](../security/asvs-l2.md): están implementados o declarados, pero nadie los ha
visto funcionar fuera de este equipo.

**Qué es esto:** la lista de comprobaciones con su comando y su resultado esperado, para
que verificar sea un rato de trabajo y no una investigación. Cada fila tiene su
identificador `SEC-xx` en [PENDING-ITEMS.md](../PENDING-ITEMS.md).

**Cómo se registra:** al comprobar una fila, cambiar su estado en `asvs-l2.md` a **cumple**
con la fecha y quién la verificó, y anotar aquí abajo la salida relevante (sin secretos).

---

## 1. Llaves de Data Protection protegidas (SEC-01 · ASVS 1.6.2)

Las llaves cifran la cookie y el ticket de sesión. Viven en el volumen `/keys` del pod de
la capa Channel (`DataProtection:KeysDirectory`).

```bash
kubectl -n modernization-score exec deploy/channel-api -- ls -l /keys
```

- **Se espera:** archivos `key-*.xml` que sobreviven al reinicio del pod.
- **Falta comprobar:** que el volumen esté cifrado en reposo y, si la plataforma lo ofrece,
  que las llaves se protejan con el vault (`ProtectKeysWithAzureKeyVault` o equivalente).
- **Prueba de humo:** reiniciar el pod y confirmar que una sesión abierta sigue válida.

## 2. mTLS entre las capas (SEC-02 · ASVS 1.9.2) y cifrado interno (SEC-10 · ASVS 9.2.2)

```bash
linkerd viz edges deployment -n modernization-score
```

- **Se espera:** la arista `channel-api → system-api` con `SECURED=√`.
- **También:** la cadena de conexión de la base y la de Redis exigen TLS.
- **Negativo:** un pod fuera del mesh no debe poder hablar con `system-api`; lo impide la
  NetworkPolicy de `deploy/k8s/network-policies.yaml`.

## 3. Gestor de secretos (SEC-05 · ASVS 6.4.1)

```bash
kubectl -n modernization-score exec deploy/system-api -- ls /mnt/secrets
```

```bash
kubectl -n modernization-score get pod -l app.kubernetes.io/name=channel-api -o jsonpath='{.items[0].spec.containers[0].env[*].name}'
```

- **Se espera:** los secretos aparecen como archivos del CSI driver, y **ningún** nombre de
  variable de entorno contiene `Token`, `Secret`, `Password` ni `ConnectionString`.
- **Rotación:** seguir el [runbook de rotación](rotacion-de-secretos.md) y comprobar que no
  hay corte de servicio.

## 4. TLS del ingress (SEC-08, SEC-09 · ASVS 9.1.2, 9.1.3)

```bash
docker run --rm drwetter/testssl.sh --protocols --cipher-per-proto https://modscore.example.com
```

- **Se espera:** TLS 1.2 y 1.3 habilitados; TLS 1.0, 1.1 y SSLv3 deshabilitados; sin suites
  con RC4, 3DES, NULL ni export; HSTS presente.
- **Alternativa sin Docker:** `nmap --script ssl-enum-ciphers -p 443 modscore.example.com`.

## 5. MFA en las interfaces administrativas (SEC-04 · ASVS 4.3.1)

La aplicación exige `ingestion.write` para cargar y `ruleset.admin` con alcance completo
para administrar reglas, y una reautenticación de los últimos 10 minutos para publicar.
**Exigir MFA a esos dos roles es política del IdP o de APIM**, no del código.

- **Se espera:** la política de acceso condicional exige MFA a los grupos que otorgan esos
  dos permisos, y publicar una versión de reglas vuelve a pedir el segundo factor.
- **Se registra:** captura de la política y el evento de auditoría 4004 tras publicar.

## 6. Reglas del SIEM por EventId (SEC-11 · ASVS 11.1.8)

Los eventos salen en la categoría `Modernization.Audit` con identificadores estables, de
modo que la alerta no dependa del texto.

| EventId | Evento | Alerta sugerida |
|---|---|---|
| 4001, 4002, 4003 | Sesión iniciada, cerrada, expirada | Sin alerta; contexto |
| 4004 | Reautenticación completada | Sin alerta; contexto de una publicación |
| 4101 | Acceso denegado | 5 en 5 minutos por usuario |
| 4102 | CSRF rechazado | Cualquiera, si se repite |
| 4103 | Límite de peticiones alcanzado | 3 en 5 minutos por usuario |
| 4104 | Operación sensible sin reautenticación reciente | Cualquiera |
| 4201, 4202 | Carga publicada, carga rechazada | Rechazos repetidos del mismo usuario |
| 4203 | Alcance insuficiente en la capa System | **Cualquiera**: la capa Channel ya debería haberlo cortado |
| 4211, 4212 | Reglas publicadas, publicación rechazada | Cualquier publicación, a revisión |
| 4213 | Datos exportados | Ráfagas: más de 20 exportaciones en 10 minutos por usuario |
| 4214 | Entrada rechazada por validación | 20 en 5 minutos por usuario: alguien está probando el borde |
| 5303 | Almacén de reglas no disponible | Cualquiera |
| 5401, 5402 | Calentamiento del portafolio, y su fallo | 5402 repetido |

- **Se espera:** las reglas existen en el SIEM, disparan en una prueba controlada y avisan
  al canal del equipo.

## 7. Integridad de la configuración y firma de imágenes (SEC-13 · ASVS 14.1.5)

Checkov ya corre en el CI sobre `deploy/`. Falta la cadena de publicación (H-17):

```bash
cosign verify --certificate-identity-regexp '.*' --certificate-oidc-issuer-regexp '.*' registry.example.com/modernization/channel-api@sha256:...
```

- **Se espera:** la firma verifica y la política de admisión del cluster rechaza una imagen
  sin firmar. Mientras no exista registro, el requisito sigue en la Fase 17.

## 8. Secretos de integración no estáticos (SEC-03 · ASVS 2.10.1)

Depende de la Fase 15: con APIM (opción C) o identidad de carga de trabajo, el token
estático entre capas desaparece.

- **Se espera:** ninguna configuración contiene un token compartido de larga vida, y la
  capa System rechaza una llamada sin credencial de vida corta válida.

---

## Lo que ya no depende de infraestructura

Estos requisitos se cerraron con código y pruebas en la Fase 14, y se verifican corriendo
la suite:

| Requisito | Qué lo cierra | Prueba |
|---|---|---|
| 7.1.3 (SEC-06) | Evento 4214 en toda entrada rechazada por validación | `ValidationAuditTests` |
| 8.3.4 (SEC-07) | `Uploads:RetainVersions` además de `Database:RetainVersions` | `FileUploadRetentionTests` |
| 13.2.2 (SEC-12) | Contrato tipado estricto ([ADR 0021](../adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md)) | Pruebas de borradores inválidos |
| 14.2.6 (SEC-14) | Controles compensatorios documentados | Escaneos del pipeline |
