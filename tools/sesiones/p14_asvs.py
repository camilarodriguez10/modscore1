"""Fase 14 - matriz ASVS: cierra 7.1.3, 8.3.4 y 13.2.2, y documenta 14.2.6."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if old not in text:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:160])
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


asvs = "docs/security/asvs-l2.md"

patch(asvs, [
    ("**Versión:** Fase 13 · **Fecha:** 2026-09-14", "**Versión:** Fase 14 · **Fecha:** 2026-09-15"),
    (
        "- Los **14 parciales** se verifican en la **Fase 14**, en un entorno de prueba, con la\n  primera corrida del pipeline.",
        "- Los **11 parciales** que quedan se verifican en un entorno de prueba: dependen de un\n  cluster, un IdP, un ingress y un gestor de secretos reales.",
    ),
    (
        "La Fase 13 no cambia los totales y agrega evidencia:",
        """La Fase 14 cierra tres parciales, documenta los controles compensatorios de un cuarto y
agrega verificación automática de accesibilidad:

- **7.1.3:** toda entrada rechazada por validación deja el evento 4214, con la ruta y el
  código y sin los valores enviados (SEC-06).
- **8.3.4:** el adaptador de archivos también tiene retención declarativa,
  `Uploads:RetainVersions`, igual que el de base de datos (SEC-07).
- **13.2.2:** contrato tipado estricto en lugar de esquema formal, decidido y registrado en
  el [ADR 0021](../adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md) (SEC-12).
- **14.2.6:** sin sandbox por librería, con controles compensatorios aceptados (SEC-14).
- **Accesibilidad:** axe recorre cinco pantallas en claro y en oscuro dentro del CI, y falla
  con violaciones serias o críticas (TD-04, TD-05).

La Fase 13 no cambia los totales y agrega evidencia:""",
    ),
    (
        "| 7.1.3 | Eventos de seguridad registrados | Parcial | Sesión, acceso denegado, CSRF, rate limit, cargas, alcance, reglas publicadas y rechazadas (4211, 4212), reautenticación (4004, 4104) y exportaciones con su cantidad de filas (4213); las validaciones rechazadas (400) no se auditan (Fase 14, SEC-06) |",
        "| 7.1.3 | Eventos de seguridad registrados | Cumple | Sesión, acceso denegado, CSRF, rate limit, cargas, alcance, reglas publicadas y rechazadas (4211, 4212), reautenticación (4004, 4104), exportaciones con su cantidad de filas (4213) y entradas rechazadas por validación (4214), con ruta y código y sin los valores enviados · `ValidationAuditTests` |",
    ),
    (
        "| 8.3.4 | Política de retención de datos sensibles | Parcial | Retención manual de versiones (plan 7bis); política declarativa en la Fase 09 |",
        "| 8.3.4 | Política de retención de datos sensibles | Cumple | `Database:RetainVersions` y `Uploads:RetainVersions` conservan las N cargas más recientes en los dos adaptadores; 0 conserva todas · `FileUploadRetentionTests`. Cuántas conservar lo fija el negocio (OP-03) |",
    ),
    (
        "| 13.2.2 | Validación de esquema JSON | Parcial | DTOs tipados y comprobaciones explícitas; sin validador de esquema formal |",
        "| 13.2.2 | Validación de esquema JSON | Cumple | Contrato tipado estricto con `BoundedJson` (128 KB, sin propiedades desconocidas, sin nulos donde el contrato no los admite, profundidad 16) más las invariantes de `Ruleset.Validate` · [ADR 0021](../adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md) |",
    ),
    (
        "| 14.2.6 | Aislamiento de componentes de terceros | Parcial | Contenedores y NetworkPolicy; sin sandbox por librería |",
        "| 14.2.6 | Aislamiento de componentes de terceros | Parcial | Sin sandbox por librería: ni .NET ni Node lo ofrecen. Controles compensatorios aceptados: árbol mínimo y bloqueado (`packages.lock.json`, `npm ci --ignore-scripts`), NuGetAudit, `npm audit`, OSV y Trivy en cada push, imágenes chiseled sin shell con usuario no root, sistema de archivos de solo lectura y capacidades descartadas, NetworkPolicy y CSP estricta sin recursos de terceros (SEC-14) |",
    ),
    (
        """| V7 Errores y logs | 8 | 1 | 1 | 0 |
| V8 Protección de datos | 4 | 1 | 0 | 0 |""",
        """| V7 Errores y logs | 9 | 0 | 1 | 0 |
| V8 Protección de datos | 5 | 0 | 0 | 0 |""",
    ),
    (
        "| V13 API | 7 | 1 | 0 | 0 |",
        "| V13 API | 8 | 0 | 0 | 0 |",
    ),
    (
        "| **Total** | **103** | **14** | **3** | **10** |",
        "| **Total** | **106** | **11** | **3** | **10** |",
    ),
    (
        """**Ningún requisito aplicable queda sin estado.** De los tres pendientes, dos son de
severidad baja (H-20, H-21) y el tercero —logs en un destino inmutable— es
infraestructura (H-17). Los parciales dependen de verificarse en un despliegue real:
mTLS, TLS del ingress, gestor de secretos y reglas del SIEM.""",
        """**Ningún requisito aplicable queda sin estado.** De los tres pendientes, dos son de
severidad baja (H-20, H-21) y el tercero —logs en un destino inmutable— es
infraestructura (H-17). Los once parciales dependen de verificarse en un despliegue real:
llaves protegidas, mTLS, gestor de secretos, TLS del ingress, cifrado interno, reglas del
SIEM, firma de imágenes y MFA exigido por el IdP. El runbook
[verificación ASVS](../runbooks/verificacion-asvs.md) dice qué comprobar y con qué comando,
para que la verificación sea un rato de trabajo y no una investigación.""",
    ),
])
