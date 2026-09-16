"""Fase 15 - PLAN, HANDOFF y conteos de pruebas."""
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


seccion = """### 8.8 Resultado de la Fase 15 — Autenticación con APIM

En curso desde el 2026-09-16. Se eligió la **opción C junto con A** del
[ADR 0020](docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md) y se dejó el código listo,
probado con dobles: APIM todavía no existe.

**La credencial entre capas ahora es conectable** ([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)):

| Pieza | Hoy | Con APIM |
|---|---|---|
| Qué manda la capa Channel | El secreto del gestor de secretos | Token de vida corta con `client_credentials`, cacheado y renovado un minuto antes de vencer |
| Qué valida la capa System | Comparación en tiempo constante, con token de rotación | Firma, emisor, audiencia, vencimiento, algoritmo de una lista blanca y cliente permitido |
| Cómo se elige | `ServiceToken:Mode` y `SystemApi:Credential:Mode` | Igual, cambiando el valor |

**Tres decisiones dentro de esa:**

1. **Modo `Both` para la ventana del cambio.** La capa System acepta el secreto compartido
   y el token de APIM a la vez, así que las dos capas no tienen que cambiar en el mismo
   instante. Sin eso, el cambio pediría un corte, y un corte invita a hacerlo mal.
2. **La credencial se pide por petición.** Un token de vida corta cambia; una cabecera
   fijada al arrancar se queda vieja. Lo pone un `DelegatingHandler` — y la primera versión
   se olvidó del cliente de cargas, cosa que encontraron las pruebas.
3. **El token no se registra nunca**, ni siquiera cuando el emisor responde con error: el
   mensaje lleva el código de estado, no el cuerpo.

**Reautenticación y alcance:**

- `ApimReauthentication` cierra la sesión anterior y sale al mismo flujo de código de
  autorización, con los parámetros que APIM espera (`acr_values`, por ejemplo). Exigir MFA
  sigue siendo política del IdP o de APIM, no del código (SEC-04).
- Si el IdP no puede emitir el claim de alcance, se mapean grupos
  (`Authorization:ScopeFromGroups`). **El claim manda sobre los grupos:** sumarlos podría
  ampliar un alcance ya emitido, y eso no puede pasar por accidente (H-02).

**Medido en pruebas:** 20 llamadas simultáneas al vencer el token piden **una sola** vez el
token nuevo; un token de otra audiencia, de otro emisor, vencido, sin firma o firmado con
otra llave se rechaza; el cuerpo del error del emisor no llega al mensaje.

**Lo que falta, y por qué:** el emisor. Con APIM real hay que verificar que el token traiga
la audiencia de la capa System y un `azp` estable, que la reautenticación llegue al IdP
(SEC-19) y que el alcance entregue lo que el negocio espera. El cambio, paso a paso y con
su vuelta atrás, está en el [runbook de paso a APIM](docs/runbooks/paso-a-apim.md).

## 8bis. Ampliación solicitada — fases 10 a 12"""

patch("PLAN.md", [
    ("## 8bis. Ampliación solicitada — fases 10 a 12", seccion),
    (
        "| **15 — Autenticación con APIM** | 3 sem | OAuth mediante Azure API Management ([ADR 0020](docs/adr/0020-camino-de-oidc-a-oauth-con-apim.md)), claim de alcance, token de servicio reemplazado | Reautenticación verificada contra el IdP real; sin secreto estático entre capas |",
        "| **15 — Autenticación con APIM** 🟡 | EN CURSO | Opción C con A elegida el 2026-09-16 ([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)): credencial conectable, validación del JWT en la capa System, modo `Both` para cambiar sin corte, reautenticación de APIM y alcance desde grupos (ver 8.8) | **Parcial:** el código está probado con dobles; cerrar H-16 espera a que APIM emita los tokens y el secreto salga del gestor |",
    ),
])

patch("HANDOFF.md", [
    (
        "| Fase 14 — verificación de seguridad | **En curso**",
        "| Fase 15 — autenticación con APIM | **En curso** (PLAN §8.8). Elegida la opción C con A ([ADR 0022](docs/adr/0022-la-credencial-entre-capas-es-conectable.md)): la credencial entre capas es conectable, la capa System valida un JWT de APIM y hay un modo `Both` para cambiar sin corte. Falta el emisor real; el cambio está en [su runbook](docs/runbooks/paso-a-apim.md) |\n| Fase 14 — verificación de seguridad | **En curso**",
    ),
    (
        "| Pruebas | **646 .NET** (200 motor · 178 contratos · 268 API)",
        "| Pruebas | **666 .NET** (200 motor · 178 contratos · 288 API)",
    ),
])

patch("docs/PENDING-ITEMS.md", [
    (
        "  - 646 .NET tests: 200 engine, 178 contracts, 268 API.",
        "  - 666 .NET tests: 200 engine, 178 contracts, 288 API.",
    ),
    (
        "| API end-to-end through both layers and security | 265 | `dotnet test tests/Modernization.Api.Tests` |",
        "| API end-to-end through both layers and security | 288 | `dotnet test tests/Modernization.Api.Tests` |",
    ),
    (
        "| Source contracts (Dummy, File, Database; rules stores) | 175 | `dotnet test tests/Modernization.Sources.ContractTests` |",
        "| Source contracts (Dummy, File, Database; rules stores) | 178 | `dotnet test tests/Modernization.Sources.ContractTests` |",
    ),
])
