"""Fase 12: matriz ASVS con la reautenticacion y la administracion de reglas."""
import io

PATH = "C:/Users/kmiro/Downloads/mod score/docs/security/asvs-l2.md"

raw = io.open(PATH, encoding="utf-8", newline="").read()
crlf = "\r\n" in raw
s = raw.replace("\r\n", "\n")

pairs = [
    ("**Versión:** Fase 08 · **Fecha:** 2026-09-11", "**Versión:** Fase 12 · **Fecha:** 2026-09-12"),
    ("| V3 Sesiones | 10 | 0 | 1 | 3 |", "| V3 Sesiones | 11 | 0 | 1 | 2 |"),
    ("| V4 Control de acceso | 7 | 2 | 0 | 0 |", "| V4 Control de acceso | 8 | 1 | 0 | 0 |"),
    ("| **Total** | **101** | **15** | **3** | **11** |", "| **Total** | **103** | **14** | **3** | **10** |"),
    (
        "| 1.5.3 | Validación de entrada en una capa de confianza | Cumple | Validación en ambas capas: EAI, claves de palanca, filtros, etiqueta |",
        "| 1.5.3 | Validación de entrada en una capa de confianza | Cumple | Validación en ambas capas: EAI, claves de palanca, filtros, etiqueta y borradores de reglas (JSON estricto y acotado; la validación de la regla vive junto al motor) |",
    ),
    (
        "| 1.11.2 | Flujos de alto valor sin condiciones de carrera | Cumple | `AnUploadDuringALoadIsNotOverwrittenByTheStaleRead` · `OnlyOnePublicationRunsAtATime` |",
        "| 1.11.2 | Flujos de alto valor sin condiciones de carrera | Cumple | `AnUploadDuringALoadIsNotOverwrittenByTheStaleRead` · `OnlyOnePublicationRunsAtATime` · reglas con concurrencia optimista (`APublicationPreparedOnAnOlderVersionIsAConflict`) |",
    ),
    (
        "| 3.7.1 | Reautenticación antes de operaciones sensibles | N/A | Overrides y publicación de rulesets aún sin interfaz; se exige al construirlas |",
        "| 3.7.1 | Reautenticación antes de operaciones sensibles | Cumple | Publicar reglas exige haberse autenticado en los últimos 10 minutos: con OIDC, `max_age=0` y un `auth_time` reciente. La System exige además el permiso reenviado · `PublishingNeedsARecentSignInAndStepUpGrantsIt` |",
    ),
    (
        "| 4.3.3 | Protección adicional en operaciones de alto valor | Parcial | Rol, alcance completo, frecuencia baja y auditoría; sin reautenticación explícita |",
        "| 4.3.3 | Protección adicional en operaciones de alto valor | Cumple | Cargas: rol, alcance completo, frecuencia baja y auditoría. Reglas: además, reautenticación reciente, simulación del impacto antes de publicar y permisos que la System vuelve a exigir ([ADR 0018](../adr/0018-las-reglas-se-publican-como-versiones.md)) |",
    ),
]

for old, new in pairs:
    count = s.count(old)
    assert count == 1, (count, old[:80])
    s = s.replace(old, new)

if crlf:
    s = s.replace("\n", "\r\n")

io.open(PATH, "w", encoding="utf-8", newline="").write(s)
print("ok asvs")
