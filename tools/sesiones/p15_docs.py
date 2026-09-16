"""Fase 15 - documentacion: ADR 0020, ASVS, amenazas, hallazgos, pendientes y README."""
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


# --------------------------------------------------------------- ADR 0020
patch("docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md", [
    (
        "**Estado:** aceptada · **Fecha:** 2026-09-14 · **Relacionado:** ADR 0005, ADR 0011, ADR 0013, RFC-12",
        "**Estado:** aceptada · **Fecha:** 2026-09-14 · **Relacionado:** ADR 0005, ADR 0011, ADR 0013, RFC-12\n\n> **2026-09-16:** se eligio la opcion **C junto con A**, y el codigo quedo listo detras de\n> interfaces. Ver [ADR 0022](0022-la-credencial-entre-capas-es-conectable.md) y el\n> [runbook de paso a APIM](../runbooks/paso-a-apim.md).",
    ),
])

# ------------------------------------------------------------------- ASVS
patch("docs/security/asvs-l2.md", [
    (
        "| 2.10.1 | Secretos de integración no estáticos | Parcial | Token compartido con rotación sin corte y mTLS; tokens de vida corta pendientes (H-16) |",
        "| 2.10.1 | Secretos de integración no estáticos | Parcial | Token compartido con rotación sin corte y mTLS. Desde la Fase 15 la credencial es conectable: `ClientCredentialsServiceCredential` pide un token de vida corta y `JwtServiceTokenValidator` lo valida (firma, emisor, audiencia, vencimiento, algoritmo y cliente) · `JwtServiceTokenValidatorTests` · `ClientCredentialsServiceCredentialTests`. Pasa a cumple cuando APIM emita y el secreto salga del gestor ([ADR 0022](../adr/0022-la-credencial-entre-capas-es-conectable.md), H-16) |",
    ),
])

# -------------------------------------------------------- modelo de amenazas
patch("docs/security/modelo-de-amenazas.md", [
    (
        "| **S** | Llamar a la System sin ser la Channel | Token de servicio en tiempo constante · mTLS del mesh · NetworkPolicy solo desde channel-api | `SystemEndpointsRejectRequestsWithoutAServiceToken` · `network-policies.yaml` · `namespace.yaml` | Token compartido y estático, no de vida corta (H-16) |",
        "| **S** | Llamar a la System sin ser la Channel | Token de servicio en tiempo constante · mTLS del mesh · NetworkPolicy solo desde channel-api. Desde la Fase 15 el token puede ser uno de vida corta emitido por APIM, validado por firma, emisor, audiencia y vencimiento | `SystemEndpointsRejectRequestsWithoutAServiceToken` · `JwtServiceTokenValidatorTests` · `network-policies.yaml` | Mientras el modo sea `Static`, el secreto sigue siendo compartido y sin vencimiento (H-16) |",
    ),
])

# --------------------------------------------------------------- hallazgos
patch("docs/security/hallazgos.md", [
    (
        "### H-16 · Token de servicio estático y compartido · **Parcial**",
        "### H-16 · Token de servicio estático y compartido · **Parcial**\n\n> **Fase 15 (2026-09-16):** el código ya acepta un token de vida corta en las dos puntas y\n> hay un modo `Both` para cambiar sin corte ([ADR 0022](../adr/0022-la-credencial-entre-capas-es-conectable.md),\n> [runbook](../runbooks/paso-a-apim.md)). El hallazgo se cierra cuando APIM emita los tokens\n> y el secreto compartido salga del gestor de secretos.",
    ),
])

# ------------------------------------------------------------ PENDING-ITEMS
patch("docs/PENDING-ITEMS.md", [
    (
        "| PA-02 | Authentication through Azure API Management with OAuth (ADR 0020) | Platform / security | P0 | Platform + identity + security | Decision needed |",
        "| PA-02 | Authentication through Azure API Management with OAuth (ADR 0020) | Platform / security | P0 | Platform + identity + security | Option decided on 2026-09-16 (C with A); code ready, waiting for APIM |",
    ),
    (
        "| PA-03 | Portfolio scope claim (RFC-10) | Platform / security | P0 | Identity | Decision needed |",
        "| PA-03 | Portfolio scope claim (RFC-10) | Platform / security | P0 | Identity | Code ready: claim, or a group-to-scope map; identity has to confirm which |",
    ),
    (
        "| PA-05 | Workload identity Channel → System (RFC-12, H-16) | Platform / security | P1 | Platform + security | Open |",
        "| PA-05 | Workload identity Channel → System (RFC-12, H-16) | Platform / security | P1 | Platform + security | Code ready (client credentials + JWT validation); waiting for the issuer |",
    ),
    (
        "| SEC-03 | 2.10.1 | Integration secrets are not static | Shared token with seamless rotation and mTLS | Short-lived workload identity (PA-05) or APIM (PA-02 C) | No static secret in configuration | M |",
        "| SEC-03 | 2.10.1 | Integration secrets are not static | Shared token with seamless rotation and mTLS; since phase 15 the credential is pluggable and the System layer validates a short-lived JWT, with a `Both` mode for the cutover | The issuer: APIM or the IdP. Closing it means `ServiceToken:Mode=Jwt` and removing the shared secret | No static secret in configuration; `JwtServiceTokenValidatorTests` already covers the validation | M |",
    ),
])

# ------------------------------------------------------------------ README
patch("README.md", [
    (
        "| `Uploads__RetainVersions` | System | 0 | Cuántas cargas conserva el adaptador de archivos; 0 conserva todas |",
        """| `Uploads__RetainVersions` | System | 0 | Cuántas cargas conserva el adaptador de archivos; 0 conserva todas |

## Autenticación y credencial entre capas

Hoy: OIDC para las personas y un secreto compartido entre las dos capas. Con APIM
([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)), las dos cosas
cambian por configuración, sin desplegar otra versión. El paso a paso, en el
[runbook](docs/runbooks/paso-a-apim.md).

| Variable | Capa | Por defecto | Para qué |
|---|---|---|---|
| `ServiceToken__Mode` | System | `Static` | Qué acepta: el secreto compartido, `Jwt` de APIM, o `Both` durante el cambio |
| `ServiceToken__Jwt__Authority` · `__Issuer` · `__Audience` | System | — | De dónde salen las llaves y qué emisor y audiencia se exigen |
| `ServiceToken__Jwt__AllowedCallers__0` | System | vacío | Clientes aceptados (`azp`); vacío acepta cualquiera que el emisor autorice |
| `SystemApi__Credential__Mode` | Channel | `Static` | `ClientCredentials` pide un token de vida corta y lo renueva |
| `SystemApi__Credential__TokenEndpoint` · `__ClientId` · `__Scope` | Channel | — | Emisor, cliente y audiencia. El secreto va como archivo `SystemApi__Credential__ClientSecret` |
| `Authentication__Provider` | Channel | `Oidc` | `Apim` usa el flujo de reautenticación de APIM |
| `Authentication__Apim__AuthorizeParameters__*` | Channel | vacío | Parámetros extra de la autorización, por ejemplo `acr_values=mfa` |
| `Authorization__ScopeFromGroups__ClaimType` · `__Map__*` | Channel | vacío | Traduce grupos del IdP a alcance cuando no hay claim propio; el claim manda sobre los grupos |""",
    ),
])
