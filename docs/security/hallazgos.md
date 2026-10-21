# Registro de hallazgos — Fase 08, revisión de las fases 09 a 13 y primer pipeline

**Fecha:** 2026-09-11, ampliado el 2026-09-14 y el 2026-09-15 · **Criterio de aceptación de la Fase 08:** sin hallazgos High ni Critical abiertos

> **Decisión del 2026-09-14:** H-16, H-17, H-20 y H-21 se resuelven en la **Fase 17**, al
> final del plan. La revisión de las fases 09 a 13 y la del primer pipeline están al final
> de este documento.

## Resultado

| Severidad | Encontrados | Cerrados | Parciales | Pendientes |
|---|---:|---:|---:|---:|
| Crítica | 0 | — | — | — |
| **Alta** | **3** | **3** | 0 | 0 |
| Media | 10 | 8 | 1 | 1 |
| Baja | 8 | 6 | 0 | 2 |
| Informativa | 2 | 2 | 0 | 0 |
| **Total** | **23** | **19** | **1** | **3** |

**Criterio cumplido: ninguna Alta ni Crítica abierta.** Las tres Altas se
encontraron en esta fase y se cerraron con pruebas que las reproducen. Lo que
queda abierto depende de infraestructura que no existe fuera de un despliegue
real (H-16, H-17) o son requisitos ASVS de severidad baja (H-20, H-21).

## Cómo se buscó

| Técnica | Qué cubrió |
|---|---|
| Revisión de código contra el plan (secc. 6.3 y 7) y ASVS L2 | Autorización, sesión, límites, errores, cadena de suministro |
| Pruebas de ataque automatizadas | 84 pruebas nuevas; cada hallazgo cerrado tiene la suya |
| Verificación en vivo contra Kestrel | Lo que el servidor de pruebas en memoria no reproduce: banner, límites de cuerpo, CSP en navegador |
| SAST | Analizadores .NET con **todas** las reglas de seguridad; `npm audit`; `dotnet list package --vulnerable` |
| Prueba de carga | `tools/Modernization.LoadTest`, antes y después de cada corrección de rendimiento |

**Lo que no se hizo, y por qué:** DAST con OWASP ZAP y escaneo de imágenes con
Trivy requieren descargar sus imágenes de contenedor; quedan en el pipeline de CI
y a ejecutar localmente con autorización. Las imágenes propias no se construyeron
por la misma razón (H-17).

## Criterio de severidad

| Severidad | Cuándo |
|---|---|
| **Crítica** | Explotable sin autenticación, con compromiso de todos los datos o de su integridad |
| **Alta** | Cualquier usuario autenticado compromete la confidencialidad o la integridad del portafolio de la organización, o degrada el servicio para todos |
| **Media** | Requiere una condición adicional —una cookie obtenida, una carrera, volumen sostenido— o afecta a una parte |
| **Baja** | Reduce la defensa en profundidad o entrega información de reconocimiento |
| **Informativa** | No explotable por sí sola; debilidad de proceso |

---

## Altas

### H-01 · Cualquier sesión podía publicar una carga · **Cerrado**

Publicar reemplaza el portafolio de toda la organización y solo exigía estar
autenticado. Cualquier empleado con acceso de lectura podía cambiar la base de las
decisiones de inversión.

- **Corrección:** política `ingestion.write` que exige el rol **y** alcance
  completo; la capa System vuelve a exigir alcance completo; la pantalla explica
  qué permiso falta.
- **Evidencia:** `AReaderCannotPublishAnUpload` · `UploadScreenExplainsThePermissionThatIsMissing` · `UploadRequiresTheFullScope`

### H-02 · Sin autorización sobre los datos · **Cerrado**

El plan prometía filtrado por LOB y CIO en la consulta; no existía. Todo usuario
veía el portafolio completo, **incluidas las listas de CVEs y librerías vulnerables
por aplicación**: un mapa de qué atacar primero.

- **Corrección:** alcance aplicado en `PortfolioService` con el modelo de subsistema
  de confianza; ninguna lectura sin alcance compila; por defecto no se ve nada;
  fuera de alcance es 404 ([ADR 0012](../adr/0012-el-alcance-se-aplica-sobre-los-datos.md)).
- **Evidencia:** `LobScopeTests` (7) · `SystemScopeTests` (3) · `PortfolioScopeTests` (9) · `EveryReadHonoursTheScope`
- **Residual:** el IdP tiene que emitir el claim de alcance (RFC-10).

### H-03 · El rate limiter corría antes de autenticar · **Cerrado**

Sin identidad en el limitador, nadie tenía balde propio: todos compartían el de su
IP, que detrás del ingress es una sola. Un usuario —o un script con una sesión—
agotaba el servicio de la organización entera.

- **Corrección:** autenticación antes del limitador; partición por subject; políticas
  propias para simulaciones y cargas; `X-Forwarded-For` solo de proxies declarados
  ([ADR 0013](../adr/0013-autenticado-no-significa-inofensivo.md)).
- **Evidencia:** `AuthenticatedUsersAreLimitedPerUserNotPerAddress` · `UploadsHaveTheirOwnMuchLowerLimit` · `SimulationsHaveTheirOwnLimit`

## Medias

### H-04 · Cerrar sesión no la invalidaba · **Cerrado**

La cookie era la sesión: el logout la borraba del navegador y una copia seguía
válida ocho horas.

- **Corrección:** sesión en el servidor (`ITicketStore`), cifrada con Data
  Protection; el logout la elimina ([ADR 0011](../adr/0011-la-sesion-vive-en-el-servidor.md)).
- **Evidencia:** `LogoutInvalidatesACopiedCookie`

### H-05 · Sesión sin vencimiento absoluto · **Cerrado**

Ocho horas deslizantes: una sesión en uso no vencía nunca (ASVS 3.3.2).

- **Corrección:** 30 minutos de inactividad y 12 horas absolutas desde el inicio.
- **Evidencia:** `SessionExpiresAfterThirtyMinutesWithoutActivity` · `AnActiveSessionStillExpiresAfterTwelveHours`

### H-06 · La historia recalculaba todas las versiones en cada consulta · **Cerrado**

La pantalla de Cargas la dispara en cada visita, y el costo crecía con cada carga
publicada. Cualquier usuario podía multiplicarlo.

| Medición con 30 versiones | Antes | Después |
|---|---:|---:|
| `GET /system/history/portfolio` | 1,0 s | 0,006 s |
| `GET /system/applications/14381/history` | 0,8 s | 0,005 s |
| Bajo carga, 16 concurrentes | — | 1 725 req/s · p95 12 ms |

- **Corrección:** puntos calculados una vez por versión, que es inmutable.
- **Evidencia:** `HistoryReadsEachVersionOnlyOnce` · mediciones antes y después

### H-07 · Carrera: scores de la versión anterior servidos como vigentes · **Cerrado**

El snapshot y su caché eran dos campos. Una petición que calculaba sobre la versión
anterior podía escribir su score después de que una carga limpiara la caché; ese
valor quedaba servido hasta la carga siguiente.

- **Corrección:** snapshot y caché en un único objeto con número de generación; una
  lectura que termina después de una publicación se descarta.
- **Evidencia:** `AnUploadDuringALoadIsNotOverwrittenByTheStaleRead`

### H-08 · La carga viajaba por el cliente HTTP estándar · **Cerrado**

Diez segundos por intento, con reintentos: un archivo grande fallaba, y un reintento
tras un timeout podía publicar dos versiones.

- **Corrección:** cliente propio sin reintentos y con 5 minutos; ningún POST se
  reintenta en ningún cliente.
- **Evidencia:** carga de 40 MB publicada de extremo a extremo en 3,3 s, en vivo

### H-09 · 30 MB aceptados en cualquier endpoint · **Cerrado**

El límite por defecto de Kestrel. Además hacía inalcanzable el tope documentado de
256 MB para la carga.

- **Corrección:** 1 MB por defecto en ambas capas; la carga lo amplía en su endpoint.
- **Evidencia:** JSON de 2 MB → **413** en ambas capas, en vivo · carga de 40 MB aceptada por el transporte

### H-10 · Detalle interno reenviado al navegador · **Cerrado**

Un escenario inválido devolvía el ProblemDetails crudo de la capa System —con su
`traceId`— y el mensaje repetía la clave de palanca que envió el cliente.

- **Corrección:** solo el campo `detail`, acotado; el motor ya no repite la entrada;
  claves de palanca por lista blanca en el borde.
- **Evidencia:** `ScenarioErrorsCarryOnlyTheCuratedDetail` · `LeverKeysOutsideTheWhitelistAreRejectedAtTheEdge` · `ProductionErrorsCarryNoInternalDetail`

### H-11 · Fuentes de Google y SPA sin CSP en producción · **Cerrado**

Cada visita a una herramienta interna avisaba a un tercero de la IP y la navegación
del usuario. Además, la CSP estricta del plan era imposible con esas fuentes, y en
producción nadie servía la SPA con CSP.

- **Corrección:** fuentes desde el mismo origen; el channel-api sirve el build con
  una CSP sin `unsafe-inline`, sin `unsafe-eval` y sin orígenes de terceros.
- **Evidencia:** `ShellIsServedWithAStrictContentSecurityPolicy` · navegador contra el build real: **cero violaciones de CSP** en cinco pantallas · fuentes servidas desde `/assets` · paso de CI que falla si el build referencia terceros

### H-16 · Token de servicio estático y compartido · **Parcial**

> **Fase 15 (2026-09-16):** el código ya acepta un token de vida corta en las dos puntas y
> hay un modo `Both` para cambiar sin corte ([ADR 0022](../adr/0022-la-credencial-entre-capas-es-conectable.md),
> [runbook](../runbooks/paso-a-apim.md)). El hallazgo se cierra cuando APIM emita los tokens
> y el secreto compartido salga del gestor de secretos.

ASVS 2.10.1 pide que los secretos de integración no sean contraseñas fijas.

- **Hecho:** rotación sin corte con token secundario; mTLS del mesh con política
  `all-authenticated`; NetworkPolicy que solo permite a channel-api llegar a la System.
- **Falta:** identidad de carga de trabajo de vida corta (tokens del service account
  proyectados, o la identidad del mesh como credencial). Depende de la plataforma
  del cluster (RFC-12).
- **Evidencia:** `SecondaryTokenIsAcceptedDuringARotation` · `network-policies.yaml`

### H-17 · Cadena de publicación incompleta · **Pendiente**

- Las imágenes no se firman (cosign) ni se atestan (SLSA) porque no hay registro
  donde publicarlas.
- Los Dockerfiles y manifiestos no se construyeron ni escanearon en local: requiere
  descargar imágenes base y de herramientas.
- Los logs de auditoría no tienen aún un destino inmutable.
- **Mitigación actual:** Trivy y Checkov bloqueantes en CI; imágenes chiseled y
  fijadas por digest en los manifiestos.

## Bajas

### H-12 · OpenAPI anónimo en todos los entornos · **Cerrado**

El mapa completo del API era público.

- **Evidencia:** `ProductionDoesNotPublishTheApiMap`

### H-13 · Banner del servidor, HSTS débil y source maps · **Cerrado**

- **Corrección:** sin encabezado `Server`; HSTS de un año con subdominios y preload;
  build sin source maps; diagnósticos del runtime desactivados en la imagen.
- **Evidencia:** sin `Server` en ambas capas, en vivo · paso de CI "sin source maps"

### H-14 · Sin auditoría de seguridad · **Cerrado**

Ningún evento de seguridad se registraba, y cada carga quedaba firmada como
"usuario autenticado".

- **Corrección:** categoría `Modernization.Audit` con nueve eventos de EventId
  estable; subject seudónimo, nunca nombre ni correo; la carga registra quién la publicó.
- **Evidencia:** `APublishedVersionRecordsWhoUploadedIt` · eventos 4001, 4201 y 4202 observados en vivo

### H-15 · Entrada de la carga sin acotar · **Cerrado**

- La etiqueta aceptaba saltos de línea (líneas de log falsas) y U+202E (texto que se
  lee distinto en pantalla).
- Un campo de CSV no tenía tope de longitud.
- Nada impedía dos publicaciones simultáneas.
- Un reinicio a mitad de carga dejaba staging huérfano.
- **Evidencia:** `UploadLabelsWithControlCharactersOrExcessLengthAreRejected` · `AFieldBeyondTheLimitRejectsTheFileWithoutEchoingIt` · `OnlyOnePublicationRunsAtATime` · `InterruptedStagingIsCleanedButRecentOnesAreKept` · carga de 3,3 millones de filas rechazada por el tope, en vivo

### H-18 · Fijación de sesión en el login de desarrollo · **Cerrado**

Con sesión en el servidor, iniciar sesión sobre una cookie existente renueva la
misma clave. En producción no aplica: el callback de OIDC es cross-site y la cookie
`SameSite=Strict` no viaja en él.

- **Corrección:** en desarrollo no se firma sobre una sesión abierta; `Strict`
  queda documentada como parte del control.
- **Evidencia:** `LoginsWithoutAPreviousCookieCreateIndependentSessions` · `LoggingInAgainDoesNotSignOverTheOpenSession`

### H-20 · No se pueden ver ni cerrar las sesiones activas · **Pendiente**

ASVS 3.3.4. La sesión en el servidor lo hace posible: falta indexar las sesiones por
subject y una pantalla. Hasta entonces, revocar las llaves cierra todas.

### H-21 · Sin antivirus en las cargas · **Pendiente**

ASVS 12.4.2. Riesgo bajo: solo se aceptan CSV, se parsean como texto con topes, no
se sirven ni se ejecutan, y nadie los descarga. Se implementa si aparece un canal
de descarga.

### H-22 · Configuración de desarrollo aceptada en producción · **Cerrado**

`AllowedHosts: *` —que permite envenenar la `redirect_uri` de OIDC— y el token de
desarrollo del repositorio no impedían arrancar.

- **Evidencia:** `ProductionRefusesToStartWithoutExplicitHosts` · `ProductionRefusesTheRepositoryDevelopmentToken`

## Informativas

### H-19 · Las reglas de seguridad del analizador estaban apagadas · **Cerrado**

`AnalysisModeSecurity=All` no tiene efecto cuando `AnalysisLevel` ya define modo. Un
primer escaneo devolvió cero hallazgos; con `AnalysisLevelSecurity=latest-all`
aparecieron 64: 62 en el generador de datos dummy (deliberados, suprimidos con
justificación) y **uno real**, el sufijo aleatorio del identificador de carga con
`Random`, cambiado a `RandomNumberGenerator`.

- **Evidencia:** build con todas las reglas de seguridad y warnings como errores

### H-23 · La cookie CSRF no se borraba al cerrar sesión · **Cerrado**

Una cookie `__Host-` solo se borra con los mismos atributos con que se emitió; el
borrado sin `Secure` era ignorado por el navegador.

---

## Auditoría del 2026-10-21

El informe completo, en inglés, está en [audit-2026-10.md](audit-2026-10.md). Abarcó todo el
repositorio, incluidos los conectores a bases de datos del ADR 0037.

**Resultado:** 12 hallazgos y ninguna Alta ni Crítica abierta. Los dos últimos aparecieron el mismo día, en el pipeline.

| Id | Hallazgo | Severidad | Estado |
|---|---|---|---|
| AUD-01 | `vite` 7.3.6 vulnerable en la cadena de la SPA (servidor de desarrollo) | Alta | **Cerrado**: 7.3.7 |
| AUD-02 | Llaves de sesión (Data Protection) en claro en su volumen | Media | **Cerrado**: cifradas con certificado; fuera de Development, sin certificado no arranca |
| AUD-03 | La contraseña de un conector podía quedar en configuración en claro | Media | **Cerrado**: fuera de Development solo se acepta desde `Secrets:Directory` |
| AUD-04 | PostgreSQL aceptaba `VerifyCA` | Baja | **Cerrado**: solo `VerifyFull` |
| AUD-05 | Oracle podía apagar `SSL_SERVER_DN_MATCH` | Baja | **Cerrado** |
| AUD-06 | Imágenes base por etiqueta, no por digest | Baja | Pendiente, con H-17 (última fase) |
| AUD-07 | xUnit 2.x marcado como legado | Informativa | Aceptado: sin vulnerabilidad |
| AUD-08 | Rangos `^` en `package.json` pese a `save-exact` | Informativa | Aceptado: manda el lock con `npm ci` |
| AUD-09 | El bundle principal de la SPA pasa de 500 kB | Informativa | Pendiente: rendimiento |
| AUD-10 | `Modernization.Insights` conserva el nombre sin la pantalla | Informativa | Aceptado |
| AUD-11 | `@vitest/mocker` 5.0.0 vulnerable (avisos publicados el 2026-10-21; solo pruebas) | Baja | **Cerrado**: vitest 5.0.3 |
| AUD-12 | Contraseñas inventadas de las pruebas disparaban Gitleaks | Informativa | **Cerrado**: lista de permitidos de `.gitleaks.toml` |

## Revisión de las fases 09 a 13 (2026-09-14)

Revisión de lo construido después de la Fase 08, hecha por quien lo construyó:

- adaptador de base de datos;
- archivos reconocidos por su encabezado;
- avisos con código;
- administración de reglas con reautenticación;
- comparación de reglas;
- exportaciones;
- línea de tiempo de reglas.

**Falta la de una segunda persona**, que es el punto TD-13 de
[PENDING-ITEMS.md](../PENDING-ITEMS.md).

| Severidad | Encontrados | Cerrados | Aceptados |
|---|---:|---:|---:|
| Crítica | 0 | — | — |
| Alta | 0 | — | — |
| Media | 2 | 2 | 0 |
| Baja | 2 | 2 | 0 |
| Informativa | 3 | 2 | 1 |
| **Total** | **7** | **6** | **1** |

### H-24 · Un almacén de reglas caído dejaba sin scores al portafolio · Media · **Cerrado**

Desde la Fase 12 las reglas se leen del almacén. Si fallaba —volumen no montado, base
caída—, la excepción subía por cada petición de score y todo el portafolio respondía 500.
Una falla de infraestructura degradaba el servicio para todos.

- **Corrección:** `RulesService` sigue puntuando con las reglas vigentes y registra el
  evento 5303. Leer una versión anterior responde 503 en lugar de 404, y publicar se
  rechaza con `rules.storeUnavailable` sin activar nada que no quedó guardado.
- **Evidencia:** `RulesStoreFailureTests` (4 pruebas)

### H-25 · Exportar en CSV abre inyección de fórmulas y extracción masiva · Media · **Cerrado por diseño**

Las exportaciones de la Fase 13 escriben nombres de aplicación, CIO y otros textos de
las fuentes. Un texto que empieza con `=`, `+`, `-` o `@` se ejecuta como fórmula al
abrirse en una planilla. Además, exportar lee el portafolio completo de una vez.

- **Controles:**
  - los textos se neutralizan con apóstrofo; los números no se tocan;
  - límite propio de 10 por minuto por usuario;
  - tope de 50.000 filas;
  - evento 4213 con el tipo de exportación y las filas;
  - alcance del usuario aplicado en la capa System;
  - adjunto con `no-store` y `nosniff`.
- **Evidencia:** `TextCellsCannotBecomeFormulas` (9 casos) · `TheExportNeverWidensTheUsersScope` ·
  `ThePortfolioExportFollowsTheFiltersAndIsAudited`
- **Abierto como decisión:** etiqueta de clasificación o marca en el archivo, y roles sin
  exportación (SEC-24).

### H-26 · Con OIDC, reautenticarse dejaba viva la sesión anterior · Baja · **Cerrado**

La vuelta desde el IdP es una navegación entre sitios. La cookie `SameSite=Strict` no viaja
en ella, así que el manejador no veía la sesión anterior y no podía reemplazarla. El ticket
anterior quedaba en el almacén hasta vencer por inactividad (30 minutos): una copia robada
de esa cookie seguía sirviendo.

- **Corrección:** `IReauthenticationFlow` cierra la sesión antes de ir al IdP, sea cual sea
  el proveedor ([ADR 0020](../adr/0020-camino-de-oidc-a-oauth-con-apim.md)).
- **Evidencia:** `ReauthenticationFlowTests.Oidc_ClosesThePreviousSessionBeforeLeavingForTheIdentityProvider`

### H-27 · El almacén de reglas por archivos acumulaba staging huérfano · Baja · **Cerrado**

Una publicación interrumpida por un reinicio dejaba su directorio `.staging-*`. Nada lo
borraba.

- **Corrección:** la siguiente publicación borra los de más de 10 minutos, sin tocar ninguna
  versión ni el staging de una publicación en curso de otra réplica.
- **Evidencia:** `FileRulesStoreStagingTests`

### H-28 · La capa Channel no se podía publicar: la imagen no se habría construido · Informativa · **Cerrado**

La capa Channel referencia el proyecto de la System por sus contratos. Al publicarla, los
`appsettings*.json` de la System llegaban a la salida y chocaban con los propios
(`NETSDK1152`). Nadie lo había visto porque las imágenes nunca se construyeron fuera de CI
(H-17). Apareció al generar el paquete portable.

- **Corrección:** la Channel descarta de su publicación la configuración que llega de la
  referencia.
- **Evidencia:** paquete portable publicado y arrancado.

### H-29 · La SPA no tenía pruebas y sus tipos podían separarse del backend · Informativa · **Cerrado**

- **Corrección:**
  - 35 pruebas unitarias (Vitest);
  - 6 escenarios de punta a punta con Playwright y el Edge instalado (en la Fase 14 se sumaron 10 de accesibilidad con axe, en claro y oscuro);
  - `ViewModelContractTests`, que falla si un modelo de vista C# y su interfaz TypeScript
    difieren.

### H-30 · La línea de tiempo de reglas muestra el motivo a todos los lectores · Informativa · **Aceptado**

`GET /api/rules/timeline` da a quien consulta la versión, la fecha y el motivo escrito por
quien publicó, para marcar la tendencia y avisar de reglas nuevas. No dice quién publicó,
y una prueba lo comprueba.

- **Regla:** los motivos no llevan nombres, scores ni contexto confidencial (SEC-23).

## Primer pipeline y verificación con Docker (2026-09-15)

El primer push a GitHub disparó el workflow por primera vez. Pasaron backend, Gitleaks y
OSV. Fallaron el lint de la SPA y los dos CodeQL. El job de imágenes no corrió porque
depende de la SPA. Los jobs que nunca habían corrido se reprodujeron en local con Docker y
las mismas versiones del CI:

- Gitleaks v8.30.1 sobre toda la historia;
- build de las dos imágenes;
- Trivy;
- Checkov 3.3.17.

| Severidad | Encontrados | Cerrados | Aceptados |
|---|---:|---:|---:|
| Crítica | 0 | — | — |
| Alta | 0 | — | — |
| Media | 2 | 2 | 0 |
| Baja | 1 | 1 | 0 |
| Informativa | 3 | 2 | 1 |
| **Total** | **6** | **5** | **1** |

Resultado final en local:
- Gitleaks sin fugas;
- Trivy v0.74.0 sin vulnerabilidades altas ni críticas corregibles en ninguna imagen;
- Checkov con 188 controles de Kubernetes y 128 de Dockerfile en verde, y 2 omitidos con
  motivo.

Confirmado en el CI: la corrida #5 (`55cf36f`) pasó los 7 jobs, con CodeQL sin hallazgos
altos (SEC-20).

### H-31 · El CI instalaba un Trivy con avisos de seguridad · Media · **Cerrado**

`aquasecurity/trivy-action` está fijada por SHA en v0.36.0, que no fue afectada por el
compromiso de marzo de 2026 (GHSA-69fq-xp46-6x23). Pero la acción instala por defecto
Trivy v0.70.0, que tiene tres avisos posteriores:

- GHSA-mcj4-mphf-j9ff (alta): escritura de archivos fuera de lugar con artefactos OCI
  manipulados;
- GHSA-q3fv-x8vg-qqm4 (moderada);
- GHSA-8rc5-4fr6-64pw (moderada).

Fijar la acción no fijaba la herramienta.

- **Corrección:** `version: v0.74.0` en los dos pasos de Trivy. Es la última versión, que
  incluye las correcciones de v0.71.0, v0.71.1 y v0.72.0.
- **Evidencia:** escaneo local con `aquasec/trivy:0.74.0`.

### H-32 · CodeQL no podía publicar resultados y el gate no protegía · Media · **Cerrado**

Un repositorio privado sin GitHub Code Security no tiene code scanning: el paso `analyze`
fallaba al subir el SARIF en los dos lenguajes. Un gate que falla siempre no distingue
código con hallazgos de código limpio.

- **Corrección:** análisis con `upload: never`, SARIF como artefacto, y el job falla con
  hallazgos de nivel `error` o `security-severity` de 7 o más. El job ya no pide
  `security-events: write`. La segunda corrida (`8e4639c`) mostró que en un repositorio
  privado la acción igual lee la corrida del workflow: el job pide `actions: read`, que es
  solo lectura.
- **Evidencia:** el paso se probó con SARIF de ejemplo: sin hallazgos altos, con dos y sin
  archivo (`tools/sesiones/test-codeql-gate.mjs`).

### H-33 · Los pods corrían con un UID bajo · Baja · **Cerrado**

Los Deployments usaban el UID de la imagen, 1654. Un UID bajo puede coincidir con un
usuario del nodo (Checkov CKV_K8S_40).

- **Corrección:** `runAsUser`, `runAsGroup` y `fsGroup` en 10001. La imagen no cambia: `/app`
  es de root y legible por todos, y solo se escribe en `/tmp` y en los volúmenes.
- **Evidencia:** las dos imágenes corrieron como `10001:10001` con el sistema de archivos
  de solo lectura, sin capacidades; `/health` y la SPA respondieron sin errores de permisos.

### H-34 · Las imágenes no declaran HEALTHCHECK · Informativa · **Aceptado**

Checkov CKV_DOCKER_2. Las imágenes chiseled no tienen shell ni curl para ejecutar un
HEALTHCHECK, y Kubernetes no lo usa: la salud la vigilan las probes de `/health`.

- **Regla:** supresión en línea con el motivo en cada Dockerfile. Si las imágenes se usan
  fuera de Kubernetes, hay que revisar la decisión.

### H-35 · El paquete portable modificaba los lock files · Informativa · **Cerrado**

Publicar con `-r win-x64` agrega a 10 `packages.lock.json` una sección por runtime y
`Microsoft.NET.ILLink.Tasks`. Si se confirmaban, la restauración del CI en modo bloqueado
fallaría.

- **Corrección:** `build-portable.ps1` guarda los lock files y los devuelve al terminar
  (TD-12).
- **Evidencia:** build nuevo sin cambios en los lock files; restauración `--locked-mode` en
  verde.

### H-36 · El lint de la SPA no estaba en la verificación local · Informativa · **Cerrado**

Un BOM literal en una expresión regular de `e2e/support.ts` rompió el lint en el primer
pipeline. La verificación local de la Fase 13 no corría `npm run lint`.

- **Corrección:** el carácter se escribe como escape, y el lint entra en la verificación de
  cierre (HANDOFF, lecciones aprendidas).
- **Evidencia:** `npm run lint` sin errores.
