"""Artefacto del plan: cierre parcial de la Fase 14 (verificacion de seguridad ASVS)."""
from pathlib import Path

page = Path(
    r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score"
    r"\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\modscore-plan-live.html"
)

text = page.read_text(encoding="utf-8")


def swap(old, new):
    global text
    if new in text:
        raise SystemExit("ya aplicado: " + new[:70])
    if old not in text:
        raise SystemExit("ancla no encontrada: " + old[:110])
    text = text.replace(old, new, 1)


# ------------------------------------------------------------------ portada
swap(
    "<span>Fases 00–13 · <b>entregadas — 640 pruebas .NET, 35 unitarias y 6 e2e en verde</b></span>",
    "<span>Fases 00–13 entregadas · Fase 14 en curso · <b>646 pruebas .NET, 35 unitarias y 16 e2e en verde</b></span>",
)

# --------------------------------------------------------------------- ASVS
swap(
    "<strong>103 cumple</strong>; 14 parciales, verificables solo en un despliegue real —mTLS, TLS del ingress, gestor de secretos, SIEM—;",
    "<strong>106 cumple</strong> tras la Fase 14; 11 parciales, verificables solo en un despliegue real —llaves protegidas, mTLS, gestor de secretos, TLS del ingress, MFA del IdP, SIEM, firma de imágenes—;",
)

# --------------------------------------------------- fila de la fase 14
swap(
    '<tr><td class="mono">14</td><td>Verificación de seguridad</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>Primera corrida del pipeline con todos los gates y los 14 parciales de ASVS L2 verificados en un entorno de prueba</td></tr>',
    '<tr class="shipped"><td class="mono">14</td><td>Verificación de seguridad</td><td><span class="done">EN CURSO</span></td><td>Pipeline en verde; auditoría de validaciones rechazadas, retención de cargas, decisión sobre validación de esquema, aislamiento de terceros y accesibilidad en el CI. Quedan los once parciales que necesitan un entorno real</td></tr>',
)

# --------------------------------------------------------- pruebas en verde
swap(
    "<p><strong>640 pruebas .NET en verde</strong> en quince proyectos, más 35 unitarias y 6 de punta a punta en la SPA,",
    "<p><strong>646 pruebas .NET en verde</strong> en quince proyectos, más 35 unitarias y 16 de punta a punta en la SPA —seis de flujo y diez de accesibilidad—,",
)

# ------------------------------------------------------- seccion de fase 14
seccion = """  <h3>Fase 14 — verificación de seguridad (ASVS L2)</h3>
  <div class="ship">
    <div class="h">Lo que no depende de un cluster, hecho y probado</div>
    <p>ASVS pasa de <strong>103 a 106 requisitos que cumplen</strong> y de 14 a 11 parciales. Los que quedan necesitan un entorno real, y cada uno tiene ya su comando y su resultado esperado en el runbook de verificación: dejar de ser una investigación es parte del trabajo.</p>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:26%">Requisito</th><th>Cómo quedó</th></tr></thead>
      <tbody>
        <tr><td><strong>7.1.3 · Eventos registrados</strong></td><td>Toda entrada rechazada por validación deja el evento <span class="mono">4214</span> con la ruta y el código, y sin los valores enviados. Se audita en el pipeline, no endpoint por endpoint, para que ninguno se olvide</td></tr>
        <tr><td><strong>8.3.4 · Retención</strong></td><td><span class="mono">Uploads:RetainVersions</span> conserva las N cargas más recientes también con archivos; solo borra directorios con manifiesto, así que el almacén de reglas no se toca</td></tr>
        <tr><td><strong>13.2.2 · Validación de esquema</strong></td><td>Se queda el contrato tipado estricto en lugar de un validador de esquema JSON: una dependencia menos y una sola fuente de verdad (ADR 0021)</td></tr>
        <tr><td><strong>14.2.6 · Aislamiento de terceros</strong></td><td>Sin sandbox por librería —no lo hay en .NET ni en Node—, aceptado con controles compensatorios escritos: árbol bloqueado, escaneos en cada push, imágenes sin shell y de solo lectura, NetworkPolicy y CSP estricta</td></tr>
        <tr><td><strong>Accesibilidad</strong></td><td>axe recorre cinco pantallas en claro y oscuro dentro del CI. Encontró texto atenuado a 4,42:1, colores de estado usados como letra hasta 1,65:1 y tres barras con <span class="mono">aria-label</span> en un elemento sin rol. Los tres, corregidos</td></tr>
      </tbody>
    </table>
  </div>

"""

swap("  <div class=\"note\"><strong>Lo que sigue.</strong>", seccion + "  <div class=\"note\"><strong>Lo que sigue.</strong>")

# ------------------------------------------------------------ lo que sigue
old_note_start = "<div class=\"note\"><strong>Lo que sigue.</strong> Fase 14, verificación de seguridad con los 14 parciales de ASVS; Fase 15"
position = text.find(old_note_start)
if position == -1:
    raise SystemExit("no se encontro la nota final")
end = text.find("</div>", position) + len("</div>")

nueva_nota = (
    "<div class=\"note\"><strong>Lo que sigue.</strong> Terminar la Fase 14 en un entorno de prueba: "
    "llaves protegidas, mTLS, gestor de secretos, TLS del ingress, MFA exigida por el IdP, reglas del "
    "SIEM, firma de imágenes y ZAP contra staging; más la revisión de seguridad por una segunda persona. "
    "Después, Fase 15 con autenticación por APIM; Fase 16, escala y operación; y al final, por decisión, "
    "la Fase 17 con H-16, H-17, H-20 y H-21. Los pendientes para el equipo, en inglés y con el máximo "
    "detalle, están en <code>docs/PENDING-ITEMS.md</code>. Todo el trabajo está respaldado en un "
    "repositorio privado, con el pipeline en verde.</div>"
)

text = text[:position] + nueva_nota + text[end:]

page.write_text(text, encoding="utf-8")
print("artefacto actualizado")
