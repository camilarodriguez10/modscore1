"""Fase 14 - PLAN y HANDOFF."""
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


# --------------------------------------------------------------------- PLAN
patch("PLAN.md", [
    (
        "| **14 — Verificación de seguridad (ASVS L2)** | 3 sem | Primera corrida del pipeline con todos los gates; los 14 parciales de ASVS verificados en un entorno de prueba; revisión de seguridad de las fases 09–13 por otra persona; e2e y accesibilidad en CI | Pipeline verde; los parciales pasan a cumple o quedan con control compensatorio documentado |",
        "| **14 — Verificación de seguridad (ASVS L2)** 🟡 | EN CURSO | Lo que no necesita infraestructura, hecho el 2026-09-15 (ver 8.7): pipeline verde, auditoría de validaciones rechazadas, retención de cargas, decisión sobre validación de esquema, controles compensatorios de aislamiento, y accesibilidad y e2e en el CI. Falta el entorno de prueba para los once parciales restantes y la revisión por otra persona | **Parcial:** ASVS pasa de 103 a 106 cumple y de 14 a 11 parciales; los que quedan dependen de cluster, IdP, ingress y gestor de secretos |",
    ),
    (
        """## 8bis. Ampliación solicitada — fases 10 a 12""",
        """### 8.7 Resultado de la Fase 14 — Verificación de seguridad (ASVS L2)

En curso desde el 2026-09-15. Se hizo todo lo que no depende de un despliegue real.

**Cerrado en código, con pruebas:**

- **Validaciones rechazadas auditadas** (ASVS 7.1.3, SEC-06). Un middleware de la capa
  Channel registra el evento **4214** en toda respuesta de `/api` que rechaza la entrada
  (400, 413, 415 o 422), con la ruta y el código y **sin** los valores enviados. Va en el
  pipeline y no en cada endpoint para que ninguno se olvide.
- **Retención de cargas en archivos** (ASVS 8.3.4, SEC-07). `Uploads:RetainVersions`
  conserva las N cargas más recientes, igual que `Database:RetainVersions`; 0 conserva
  todas. Solo borra directorios con manifiesto, así que el almacén de reglas y el staging
  no se tocan. Cuántas conservar lo decide el negocio (OP-03).
- **Validación de esquema decidida** (ASVS 13.2.2, SEC-12). Se queda el contrato tipado
  estricto —`BoundedJson` más las invariantes de `Ruleset.Validate`— en lugar de un
  validador de esquema JSON, que sumaría una dependencia y una segunda fuente de verdad
  ([ADR 0021](docs/adr/0021-el-cuerpo-de-reglas-se-valida-por-contrato-tipado.md)).
- **Aislamiento de terceros** (ASVS 14.2.6, SEC-14). Aceptado como límite de diseño con
  controles compensatorios documentados: árbol mínimo y bloqueado, escaneos en cada push,
  imágenes chiseled sin shell y con solo lectura, NetworkPolicy y CSP estricta.

**Accesibilidad y pruebas (TD-04, TD-05):**

- axe recorre cinco pantallas en claro y en oscuro dentro del CI, y falla con violaciones
  serias o críticas. Encontró dos problemas reales, ya corregidos:
  - el texto atenuado quedaba en 4,42:1 sobre blanco y 3,89:1 sobre el fondo de página;
  - los colores de estado, pensados como marcas vivas, se usaban también como letra, con
    contrastes de hasta 1,65:1. Ahora hay variantes de texto que cumplen AA sin cambiar las
    marcas, y tres barras decorativas declaran `role="img"` en lugar de llevar `aria-label`
    en un `div` sin rol.
- El job **SPA de punta a punta** corre los 16 escenarios en el CI con Chromium.

**Lo que queda y por qué:** los once parciales restantes necesitan un entorno real —llaves
protegidas, mTLS, gestor de secretos, TLS del ingress, cifrado interno, MFA exigida por el
IdP, reglas del SIEM y firma de imágenes—. El runbook
[verificación ASVS](docs/runbooks/verificacion-asvs.md) deja cada uno con su comando y su
resultado esperado. La revisión de seguridad por una segunda persona (TD-13) sigue abierta.

## 8bis. Ampliación solicitada — fases 10 a 12""",
    ),
])

# ------------------------------------------------------------------ HANDOFF
patch("HANDOFF.md", [
    (
        "| Fase 13 — revisión y remediación | Entregada el 2026-09-14/15 (PLAN §8.6) |",
        "| Fase 13 — revisión y remediación | Entregada el 2026-09-14/15 (PLAN §8.6) |\n| Fase 14 — verificación de seguridad | **En curso** (PLAN §8.7). Cerrados SEC-06 (evento 4214), SEC-07 (`Uploads:RetainVersions`) y SEC-12 (ADR 0021); SEC-14 aceptado con controles compensatorios; accesibilidad y e2e en el CI. Los once parciales que quedan necesitan cluster, IdP, ingress y gestor de secretos: ver [runbook de verificación](docs/runbooks/verificacion-asvs.md) |",
    ),
    (
        "| Pruebas | **640 .NET** (200 motor · 175 contratos · 265 API) · **35 unitarias** de la SPA · **6 de punta a punta** con Playwright y Edge, contra el stack de desarrollo y contra el paquete portable · ESLint sin errores. Todo repetido en verde el 2026-09-15 |",
        "| Pruebas | **646 .NET** (200 motor · 178 contratos · 268 API) · **35 unitarias** de la SPA · **16 de punta a punta** con Playwright (6 de flujo y 10 de accesibilidad con axe, en claro y oscuro) · ESLint sin errores. Todo en verde el 2026-09-15 |",
    ),
    (
        "| ASVS L2 | 103 cumple · 14 parciales · 3 pendientes · 10 IdP o N/A |",
        "| ASVS L2 | 106 cumple · 11 parciales · 3 pendientes · 10 IdP o N/A. Los parciales dependen de un entorno real |",
    ),
])
