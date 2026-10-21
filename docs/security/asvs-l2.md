# OWASP ASVS 4.0.3 — Nivel 2

**Versión:** Fase 14 · **Fecha:** 2026-09-15

Requisitos de nivel 2 **aplicables a este sistema**, con su estado y la evidencia.
No es un certificado: es el registro de qué se verificó, cómo, y qué falta.

| Estado | Significado |
|---|---|
| **Cumple** | Implementado y verificado por test, configuración revisada o prueba en vivo |
| **Parcial** | Implementado en el código o declarado en la infraestructura, pero no verificable fuera de un despliegue real |
| **Pendiente** | No implementado. Tiene hallazgo abierto en [hallazgos.md](hallazgos.md) |
| **IdP** | Responsabilidad del proveedor de identidad corporativo |
| **N/A** | El sistema no tiene la funcionalidad a la que aplica |

## Resumen

| Capítulo | Cumple | Parcial | Pendiente | IdP · N/A |
|---|---:|---:|---:|---:|
| V1 Arquitectura | 10 | 2 | 0 | 0 |
| V2 Autenticación | 3 | 1 | 0 | 2 |
| V3 Sesiones | 11 | 0 | 1 | 2 |
| V4 Control de acceso | 8 | 1 | 0 | 0 |
| V5 Validación y codificación | 10 | 0 | 0 | 1 |
| V6 Criptografía | 4 | 1 | 0 | 0 |
| V7 Errores y logs | 9 | 0 | 1 | 0 |
| V8 Protección de datos | 5 | 0 | 0 | 0 |
| V9 Comunicaciones | 1 | 3 | 0 | 0 |
| V10 Código malicioso | 2 | 0 | 0 | 2 |
| V11 Lógica de negocio | 7 | 1 | 0 | 0 |
| V12 Archivos | 9 | 0 | 1 | 3 |
| V13 API | 8 | 0 | 0 | 0 |
| V14 Configuración | 19 | 2 | 0 | 0 |
| **Total** | **106** | **11** | **3** | **10** |

**Ningún requisito aplicable queda sin estado.** De los tres pendientes, dos son de
severidad baja (H-20, H-21) y el tercero —logs en un destino inmutable— es
infraestructura (H-17). Los once parciales dependen de verificarse en un despliegue real:
llaves protegidas, mTLS, gestor de secretos, TLS del ingress, cifrado interno, reglas del
SIEM, firma de imágenes y MFA exigido por el IdP. El runbook
[verificación ASVS](../runbooks/verificacion-asvs.md) dice qué comprobar y con qué comando,
para que la verificación sea un rato de trabajo y no una investigación.

**Plan de resolución (decidido el 2026-09-14).**

- Los **11 parciales** que quedan se verifican en un entorno de prueba: dependen de un
  cluster, un IdP, un ingress y un gestor de secretos reales.
- Los **3 pendientes** (H-17, H-20, H-21) y el parcial de H-16 van en la **Fase 17**, al
  final del plan.
- Cada requisito tiene su identificador `SEC-xx` en [PENDING-ITEMS.md](../PENDING-ITEMS.md),
  con lo que falta y cómo se verifica.

La Fase 14 cierra tres parciales, documenta los controles compensatorios de un cuarto y
agrega verificación automática de accesibilidad:

- **7.1.3:** toda entrada rechazada por validación deja el evento 4214, con la ruta y el
  código y sin los valores enviados (SEC-06).
- **8.3.4:** el adaptador de archivos también tiene retención declarativa,
  `Uploads:RetainVersions`, igual que el de base de datos (SEC-07).
- **13.2.2:** contrato tipado estricto en lugar de esquema formal, decidido y registrado en
  el [ADR 0021](../adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md) (SEC-12).
- **14.2.6:** sin sandbox por librería, con controles compensatorios aceptados (SEC-14).
- **Accesibilidad:** axe recorre seis pantallas en claro y en oscuro dentro del CI, y falla
  con violaciones serias o críticas (TD-04, TD-05).

La Fase 13 no cambia los totales y agrega evidencia:

- **4.3.1:** la reautenticación ya no depende del proveedor.
- **5.1.5:** el retorno de la reautenticación usa una lista blanca.
- **7.1.3:** las exportaciones quedan auditadas con el evento 4213.
- **Exportaciones CSV:** protegidas contra inyección de fórmulas, con límite propio y dentro del alcance del usuario (H-25 en [hallazgos.md](hallazgos.md)).

---

## V1 — Arquitectura

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 1.1.2 | Modelado de amenazas | Cumple | [modelo-de-amenazas.md](modelo-de-amenazas.md) |
| 1.1.4 | Documentación de decisiones de seguridad | Cumple | ADR 0001, 0005, 0010–0013 |
| 1.2.2 | Autenticación entre componentes | Cumple | Token de servicio en tiempo constante · `SystemEndpointsRejectRequestsWithoutAServiceToken` |
| 1.4.1 | Control de acceso en un punto de confianza | Cumple | `PortfolioService`: única vía a los datos, alcance obligatorio ([ADR 0012](../adr/0012-el-alcance-se-aplica-sobre-los-datos.md)) |
| 1.4.4 | Mecanismo único de control de acceso | Cumple | Políticas en `ChannelAuthorization` · alcance en `PortfolioScope` |
| 1.5.3 | Validación de entrada en una capa de confianza | Cumple | Validación en ambas capas: EAI, claves de palanca, filtros, eje del mapa de calor del dashboard, etiqueta y borradores de reglas, incluidas las definiciones de las seis preguntas (cada valor contra su rango en el catálogo) (JSON estricto y acotado; la validación de la regla vive junto al motor) |
| 1.6.2 | Llaves protegidas | Parcial | Data Protection con volumen propio y, desde el 2026-10-21, cifrada con un certificado del gestor de secretos (obligatorio fuera de Development, AUD-02); falta el cifrado en reposo del volumen |
| 1.7.1 | Formato de log común | Cumple | Categoría `Modernization.Audit` con EventId estables |
| 1.8.1 | Datos sensibles identificados | Cumple | Modelo de amenazas, sección "Qué se protege" |
| 1.9.2 | Comunicación cifrada entre componentes | Parcial | mTLS del mesh declarado en `namespace.yaml`; sin verificar en cluster |
| 1.11.2 | Flujos de alto valor sin condiciones de carrera | Cumple | `AnUploadDuringALoadIsNotOverwrittenByTheStaleRead` · `OnlyOnePublicationRunsAtATime` · reglas con concurrencia optimista (`APublicationPreparedOnAnOlderVersionIsAConflict`) |
| 1.12.2 | Archivos subidos fuera del alcance del servidor web | Cumple | Historia en su volumen, fuera de `wwwroot`; sin endpoint de descarga |

## V2 — Autenticación

La autenticación de personas se delega en el IdP corporativo por OIDC.

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 2.1.x · 2.3.x · 2.5.x | Contraseñas, credenciales, recuperación | IdP | — |
| 2.8.x | MFA | IdP | Acceso condicional del IdP |
| 2.2.1 | Anti-automatización | Cumple | Rate limiting por usuario · `AuthenticatedUsersAreLimitedPerUserNotPerAddress` |
| 2.10.1 | Secretos de integración no estáticos | Parcial | Token compartido con rotación sin corte y mTLS. Desde la Fase 15 la credencial es conectable: `ClientCredentialsServiceCredential` pide un token de vida corta y `JwtServiceTokenValidator` lo valida (firma, emisor, audiencia, vencimiento, algoritmo y cliente) · `JwtServiceTokenValidatorTests` · `ClientCredentialsServiceCredentialTests`. Pasa a cumple cuando APIM emita y el secreto salga del gestor ([ADR 0022](../adr/0022-la-credencial-entre-capas-es-conectable.md), H-16) |
| 2.10.3 | Secretos protegidos en el almacenamiento | Cumple | Archivos del CSI driver, nunca variables de entorno (`Secrets:Directory`) |
| 2.10.4 | Secretos fuera del código | Cumple | `ProductionRefusesTheRepositoryDevelopmentToken` · Gitleaks en CI |

## V3 — Gestión de sesiones

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 3.1.1 | Sin tokens de sesión en la URL | Cumple | Cookie `httpOnly`; ningún token en respuestas (`ResponseNeverCarriesAnAccessToken`) |
| 3.2.1 | Token nuevo al autenticar | Cumple | `SameSite=Strict` + callback OIDC cross-site ([ADR 0011](../adr/0011-la-sesion-vive-en-el-servidor.md)) · `LoginsWithoutAPreviousCookieCreateIndependentSessions` |
| 3.2.2 | Al menos 64 bits de entropía | Cumple | Clave de 256 bits con `RandomNumberGenerator` |
| 3.2.3 | Almacenamiento seguro en el navegador | Cumple | `SessionCookieIsHttpOnlySecureAndStrict` |
| 3.3.1 | Logout invalida la sesión | Cumple | `LogoutInvalidatesACopiedCookie` |
| 3.3.2 | Reautenticación a las 12 h y a los 30 min sin uso | Cumple | `AnActiveSessionStillExpiresAfterTwelveHours` · `SessionExpiresAfterThirtyMinutesWithoutActivity` |
| 3.3.3 | Cerrar otras sesiones al cambiar credenciales | IdP | — |
| 3.3.4 | Ver y cerrar las sesiones activas propias | **Pendiente** | H-20 |
| 3.4.1 | Atributo `Secure` | Cumple | `SessionCookieIsHttpOnlySecureAndStrict` |
| 3.4.2 | Atributo `HttpOnly` | Cumple | Ídem |
| 3.4.3 | Atributo `SameSite` | Cumple | Ídem |
| 3.4.4 | Prefijo `__Host-` | Cumple | Ídem |
| 3.5.x | Sesiones con tokens | N/A | El navegador no recibe tokens |
| 3.7.1 | Reautenticación antes de operaciones sensibles | Cumple | Publicar reglas exige haberse autenticado en los últimos 10 minutos: con OIDC, `max_age=0` y un `auth_time` reciente. La System exige además el permiso reenviado · `PublishingNeedsARecentSignInAndStepUpGrantsIt` |

## V4 — Control de acceso

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 4.1.1 | Aplicado en el servidor | Cumple | Políticas en la Channel, alcance en la System |
| 4.1.2 | Atributos no manipulables por el usuario | Cumple | `ABrowserCannotWidenItsScopeWithAHeader` · `OnlyAServiceCallerCanDeclareAScope` |
| 4.1.3 | Mínimo privilegio | Cumple | Roles y alcance separados; publicar exige ambos al máximo |
| 4.1.5 | Falla cerrado | Cumple | `ARequestWithoutScopeSeesAnEmptyPortfolio` · `AMissingScopeGrantsNothing` |
| 4.2.1 | Protección de referencias directas | Cumple | `AnApplicationOutsideTheScopeIsIndistinguishableFromOneThatDoesNotExist` |
| 4.2.2 | CSRF | Cumple | `MutationWithoutACsrfTokenIsRejected` |
| 4.3.1 | MFA en interfaces administrativas | Parcial | Cargar exige `ingestion.write`; administrar reglas exige `ruleset.admin` con alcance completo, y publicar, una autenticación de los últimos 10 minutos (`IReauthenticationFlow`, ADR 0020). Exigir MFA a esos dos roles es política del IdP o de APIM (Fase 14, SEC-04) |
| 4.3.2 | Sin listado de directorios ni metadatos | Cumple | Sin directory browsing; sin source maps en el build |
| 4.3.3 | Protección adicional en operaciones de alto valor | Cumple | Cargas: rol, alcance completo, frecuencia baja y auditoría. Reglas: además, reautenticación reciente, simulación del impacto antes de publicar y permisos que la System vuelve a exigir ([ADR 0018](../adr/0018-las-reglas-se-publican-como-versiones.md)) |

## V5 — Validación, sanitización y codificación

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 5.1.1 | Contaminación de parámetros | Cumple | Enlace de minimal APIs a valores únicos y tipados |
| 5.1.3 | Validación por lista blanca | Cumple | `MalformedEaiCodeIsRejectedAtTheEdge` · `LeverKeysOutsideTheWhitelistAreRejectedAtTheEdge` |
| 5.1.4 | Datos estructurados con tipos | Cumple | DTOs explícitos, nunca entidades de dominio |
| 5.1.5 | Redirecciones a destinos permitidos | Cumple | `RedirectUri` fijo en el login. La reautenticación acepta `returnTo` solo de una lista blanca de una ruta (`StepUp.SafeReturnPath`), con tres pruebas de redirección abierta |
| 5.2.1 | Sanitización de HTML no confiable | Cumple | React escapa; `dangerouslySetInnerHTML` prohibido por lint |
| 5.2.7 | Sanitización de SVG | N/A | Los SVG se generan desde datos, sin contenido de usuario |
| 5.3.1 | Codificación según el contexto de salida | Cumple | Serialización JSON; sin HTML construido en el servidor |
| 5.3.3 | Protección contra XSS reflejado | Cumple | `ErrorMessagesDoNotEchoUserInput` · `ScenarioErrorsCarryOnlyTheCuratedDetail` |
| 5.3.4 | Consultas parametrizadas | Cumple | EF Core con LINQ en todo el adaptador: no hay una sola cadena de SQL construida a mano |
| 5.3.6 | Protección contra inyección en logs | Cumple | `CleanNeutralizesLineBreaksAndFormatCharacters` · `UploadLabelsWithControlCharactersOrExcessLengthAreRejected` |
| 5.5.3 | Deserialización segura | Cumple | `System.Text.Json` sin tipos polimórficos |

## V6 — Criptografía

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 6.2.1 | Falla segura | Cumple | Ticket que no descifra = sesión inexistente (`DistributedTicketStore`) |
| 6.2.2 | Algoritmos probados | Cumple | Data Protection de ASP.NET Core; nada criptográfico propio |
| 6.3.1 | Aleatoriedad criptográfica | Cumple | Token CSRF, clave de sesión e id de carga con `RandomNumberGenerator`; CA5394 activa en el build |
| 6.4.1 | Gestor de secretos | Parcial | Montaje CSI declarado; sin verificar contra el gestor real |
| 6.4.2 | Material de llaves no expuesto | Cumple | Llaves en volumen propio, fuera de la imagen y del repositorio |

## V7 — Errores y logs

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 7.1.1 | Sin credenciales ni tokens en logs | Cumple | Se registra el subject; nunca cookie, token ni correo |
| 7.1.2 | Sin otros datos sensibles | Cumple | Sin nombre, correo ni contenido de archivos |
| 7.1.3 | Eventos de seguridad registrados | Cumple | Sesión, acceso denegado, CSRF, rate limit, cargas, alcance, reglas publicadas y rechazadas (4211, 4212), reautenticación (4004, 4104), exportaciones con su cantidad de filas (4213) y entradas rechazadas por validación (4214), con ruta y código y sin los valores enviados · `ValidationAuditTests` |
| 7.1.4 | Información suficiente para investigar | Cumple | EventId, subject, ruta, versión y hora del logger |
| 7.2.1 | Decisiones de autenticación registradas | Cumple | Eventos 4001–4003 |
| 7.2.2 | Decisiones de acceso fallidas registradas | Cumple | Eventos 4101 y 4203 |
| 7.3.1 | Codificación contra inyección en logs | Cumple | `SecurityAudit.Clean` en cada evento |
| 7.3.3 | Logs protegidos contra alteración | **Pendiente** | Destino inmutable en el SIEM: infraestructura (H-17) |
| 7.4.1 | Mensaje de error genérico | Cumple | `ProductionErrorsCarryNoInternalDetail` |
| 7.4.3 | Manejador de último recurso | Cumple | `UseExceptionHandler` en ambas capas |

## V8 — Protección de datos

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 8.1.1 | Sin datos sensibles en caches del servidor | Cumple | `Cache-Control: no-store` |
| 8.2.1 | Anti-cache en respuestas sensibles | Cumple | `PortfolioResponsesAreNotCacheable` |
| 8.2.2 | Nada sensible en el almacenamiento del navegador | Cumple | Sin tokens ni datos en `localStorage` |
| 8.3.1 | Datos sensibles en el cuerpo, no en la URL | Cumple | La URL lleva identificadores (EAI), no datos |
| 8.3.4 | Política de retención de datos sensibles | Cumple | `Database:RetainVersions` y `Uploads:RetainVersions` conservan las N cargas más recientes en los dos adaptadores; 0 conserva todas · `FileUploadRetentionTests`. Cuántas conservar lo fija el negocio (OP-03) |

## V9 — Comunicaciones

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 9.1.1 | TLS para todo cliente | Cumple | Ingress con TLS obligatorio · HSTS de un año con preload |
| 9.1.2 | Solo suites fuertes | Parcial | Configuración del ingress |
| 9.1.3 | Solo TLS 1.2 o superior | Parcial | Configuración del ingress |
| 9.2.2 | Cifrado en conexiones internas | Parcial | mTLS del mesh con política `all-authenticated`; sin verificar en cluster |

## V10 — Código malicioso

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 10.2.1 | Sin conexiones salientes no documentadas | Cumple | NetworkPolicy de salida: solo System, IdP y DNS |
| 10.2.3 | Sin puertas traseras | Cumple | El esquema de desarrollo no arranca fuera de Development |
| 10.3.1 | Integridad de actualizaciones automáticas | N/A | No hay auto-actualización |
| 10.3.2 | Subresource Integrity | N/A | Todos los recursos son del mismo origen |

## V11 — Lógica de negocio

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 11.1.1 | Pasos en orden | Cumple | Staging → validación → ensamblado → publicación |
| 11.1.2 | Límites de tiempo realistas | Cumple | Rate limiting por usuario |
| 11.1.3 | Límites por acción de negocio | Cumple | `UploadsHaveTheirOwnMuchLowerLimit` · `SimulationsHaveTheirOwnLimit` |
| 11.1.4 | Anti-automatización | Cumple | Ídem |
| 11.1.5 | Límites de la lógica | Cumple | Objetivo entre 20 y 100; hasta 20 palancas |
| 11.1.6 | Sin condiciones de carrera | Cumple | Estado con generación · publicación única |
| 11.1.7 | Monitoreo de eventos inusuales | Cumple | EventId y señales en el [runbook de incidente](../runbooks/incidente-de-seguridad.md) |
| 11.1.8 | Alertas configurables | Parcial | Reglas del SIEM por EventId: infraestructura |

## V12 — Archivos

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 12.1.1 | Archivos grandes que llenen el almacenamiento | Cumple | 128 MB por archivo y 256 MB por carga, 5 cada 10 minutos, una a la vez, staging limpiado; la memoria de una carga grande se midió contra el pod (ADR 0030) |
| 12.1.2 | Archivos comprimidos | N/A | Solo CSV |
| 12.1.3 | Cuota por usuario | Cumple | Rate limit de cargas por usuario |
| 12.2.1 | Tipo de archivo verificado | Cumple | Extensión, content-type y esquema · `OnlyCsvExtensionsAreAccepted` |
| 12.3.1 | Nombre de archivo del usuario no usado como ruta | Cumple | `PathTraversalInFileNameIsNeverUsedAsAPath` |
| 12.3.2 | Protección contra LFI | Cumple | Nombre canónico del contrato |
| 12.3.3 · 12.3.4 | RFI · SSRF por nombre | N/A | Sin URLs de usuario |
| 12.3.5 | Sin ejecución de comandos | Cumple | El contenido solo se parsea como CSV |
| 12.4.1 | Almacenamiento fuera del servidor web | Cumple | Volumen de historia, fuera de `wwwroot` |
| 12.4.2 | Escaneo antivirus | **Pendiente** | H-21 · riesgo bajo: texto parseado, nunca servido ni ejecutado |
| 12.5.1 | Servir solo extensiones esperadas | Cumple | Solo el build de la SPA |
| 12.5.2 | Archivos subidos nunca ejecutados | Cumple | No hay endpoint de descarga |
| 12.6.1 | SSRF | N/A | Sin URLs de usuario |

## V13 — API

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 13.1.3 | Sin información sensible en la URL | Cumple | Sin tokens ni claves en rutas |
| 13.1.4 | Autorización por URI y por recurso | Cumple | Política por endpoint + alcance por aplicación |
| 13.1.5 | Content-type inesperado rechazado | Cumple | `NonMultipartRequestIsRejected` · `UnexpectedContentTypeIsRejected` |
| 13.2.1 | Métodos HTTP limitados | Cumple | Solo los métodos mapeados; el resto 405 |
| 13.2.2 | Validación de esquema JSON | Cumple | Contrato tipado estricto con `BoundedJson` (128 KB, sin propiedades desconocidas, sin nulos donde el contrato no los admite, profundidad 16) más las invariantes de `Ruleset.Validate` · [ADR 0021](../adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md) |
| 13.2.3 | CSRF en APIs con cookie | Cumple | Doble envío + `SameSite=Strict` |
| 13.2.5 | Content-Type verificado | Cumple | Enlace JSON exige `application/json` |
| 13.2.6 | Tamaño de cuerpo acotado | Cumple | 1 MB por defecto, verificado en vivo con 413 |

## V14 — Configuración

| Req. | Requisito | Estado | Evidencia |
|---|---|---|---|
| 14.1.1 | Build y despliegue automatizados | Cumple | `ci.yml` · `seguridad.yml` · Dockerfiles · manifiestos |
| 14.1.2 | Advertencias del compilador y análisis | Cumple | `TreatWarningsAsErrors` · todas las reglas de seguridad (`AnalysisLevelSecurity=latest-all`) |
| 14.1.3 | Servidor endurecido | Cumple | Distroless (Azure Linux 3.0), sin shell, no root, filesystem de solo lectura, capabilities fuera, PSA `restricted` |
| 14.1.5 | Integridad de la configuración | Parcial | Checkov en CI; firma de imágenes pendiente (H-17) |
| 14.2.1 | Componentes al día y sin vulnerabilidades | Cumple | NuGetAudit · `dotnet list package --vulnerable` · `npm audit` · OSV · Renovate |
| 14.2.2 | Funcionalidad innecesaria retirada | Cumple | OpenAPI solo en Development · sin source maps · diagnósticos del runtime desactivados |
| 14.2.3 | Recursos externos con SRI | Cumple | No hay recursos externos: fuentes auto-alojadas |
| 14.2.4 | Componentes de repositorios confiables | Cumple | Lock files · `ignore-scripts` · acciones fijadas por SHA |
| 14.2.5 | Inventario de componentes | Cumple | SBOM CycloneDX en CI |
| 14.2.6 | Aislamiento de componentes de terceros | Parcial | Sin sandbox por librería: ni .NET ni Node lo ofrecen. Controles compensatorios aceptados: árbol mínimo y bloqueado (`packages.lock.json`, `npm ci --ignore-scripts`), NuGetAudit, `npm audit`, OSV y Trivy en cada push, imágenes chiseled sin shell con usuario no root, sistema de archivos de solo lectura y capacidades descartadas, NetworkPolicy y CSP estricta sin recursos de terceros (SEC-14) |
| 14.3.2 | Modos de depuración desactivados | Cumple | `DeploymentGuards` · guardia del esquema de desarrollo · `DOTNET_EnableDiagnostics=0` |
| 14.3.3 | Encabezados sin versiones | Cumple | Sin encabezado `Server`, verificado en vivo |
| 14.4.1 | Content-Type con charset | Cumple | JSON con `charset=utf-8` |
| 14.4.3 | Content-Security-Policy | Cumple | `ShellIsServedWithAStrictContentSecurityPolicy` · `ContentSecurityPolicyDeniesEverythingOnAJsonApi` |
| 14.4.4 | `X-Content-Type-Options: nosniff` | Cumple | `SecurityHeadersArePresentOnEveryResponse` |
| 14.4.5 | HSTS | Cumple | Un año, subdominios y preload fuera de Development |
| 14.4.6 | `Referrer-Policy` | Cumple | `no-referrer` |
| 14.4.7 | Protección contra framing | Cumple | `frame-ancestors 'none'` + `X-Frame-Options: DENY` |
| 14.5.2 | El Origin no decide la autorización | Cumple | CSRF por token, no por Origin |
| 14.5.3 | CORS con lista blanca | Cumple | Sin CORS en producción: mismo origen |
| 14.5.4 | Encabezados de proxy autenticados | Cumple | `X-Forwarded-*` solo de proxies declarados |
