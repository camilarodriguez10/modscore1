"""Artefacto del plan: cierre de la Fase 11."""
import io

PATH = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"

raw = io.open(PATH, encoding="utf-8", newline="").read()
crlf = "\r\n" in raw
s = raw.replace("\r\n", "\n")

SECTION = """  <h3>Fase 11 — sitio en inglés</h3>
  <div class="ship">
    <div class="h">Criterio de aceptación cumplido, con una prueba</div>
    <p><strong>Ninguna respuesta de la capa Channel trae texto en español.</strong> No lo afirma una revisión, lo comprueba una prueba: recorre la sesión, el portafolio, las siete agrupaciones del roadmap, 40 aplicaciones en sus cinco pantallas, escenarios, los tres casos de score objetivo, los errores de validación, una petición sin CSRF y cargas aceptadas y rechazadas. Además, la interfaz pasó la revisión en vivo pantalla por pantalla.</p>
  </div>

  <div class="prose">
    <p>Traducir cada cadena donde estaba habría dejado el idioma repartido entre dos capas. <strong>El backend dejó de redactar frases para personas</strong>: devuelve un código estable con sus datos —<span class="mono">{ "code": "csv.missingColumns", "args": { "missing": "EAI_CODE" } }</span>— y la interfaz arma la frase desde un único catálogo. Una prueba recorre los códigos del backend y falla si alguno no tiene frase.</p>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:30%">Tipo de texto</th><th>Dónde vive ahora</th></tr></thead>
      <tbody>
        <tr><td><strong>Frase generada desde datos</strong></td><td>El backend envía el código y los datos invariantes, y <code>notices.ts</code> redacta la frase: «3 open Critical flaws», «Required columns are missing: EAI_CODE»</td></tr>
        <tr><td><strong>Contenido de un contrato</strong></td><td>Definiciones MAPS, hitos, reglas de coherencia y notas del ruleset, traducidos en el propio contrato. La Fase 12 los hará editables desde el portal, así que no pueden vivir en el bundle</td></tr>
        <tr><td><strong>Error de API</strong></td><td><code>ProblemDetails</code> con <code>type</code> estable —<span class="mono">urn:modscore:problem:application-not-found</span>— y <code>title</code> y <code>detail</code> en inglés. El detalle interno de un error de ruleset ya no llega al usuario</td></tr>
        <tr><td><strong>Texto para quien opera</strong></td><td>Logs, comentarios, resúmenes OpenAPI y errores de arranque siguen en español, igual que la documentación</td></tr>
      </tbody>
    </table>
  </div>

  <div class="note"><strong>No se rompe nada de lo ya compartido.</strong> Los tramos pasan a códigos (<span class="mono">critical</span>, <span class="mono">at-risk</span>, <span class="mono">acceptable</span>, <span class="mono">solid</span>) y los nombres anteriores se siguen aceptando. <span class="mono">/aplicaciones/14381/calculo</span> abre <span class="mono">/applications/14381/calculation</span>. La revisión en vivo encontró dos defectos, ya cerrados: un enlace con <span class="mono">?band=Critico</span> mostraba el chip «Critico», y había conteos sin concordancia como «1 applications». <strong>524 pruebas en verde</strong>, cero paquetes vulnerables y <code>npm audit</code> limpio.</div>

"""

pairs = [
    (
        "<span>Fases 00–10 · <b>entregadas — 520 tests en verde</b></span>",
        "<span>Fases 00–11 · <b>entregadas — 524 tests en verde</b></span>",
    ),
    (
        '<tr><td class="mono">11</td><td>Sitio en inglés</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>El backend deja de emitir texto para personas; la interfaz lo traduce</td></tr>',
        '<tr class="shipped"><td class="mono">11</td><td>Sitio en inglés</td><td><span class="done">ENTREGADA</span></td><td><strong>Ninguna respuesta de la capa Channel trae texto en español</strong> · el backend envía códigos y la interfaz redacta · errores con <code>type</code> estable · los enlaces anteriores redirigen</td></tr>',
    ),
    ("<h3>Fases 00–10 — entregadas</h3>", "<h3>Fases 00–11 — entregadas</h3>"),
    (
        "<p><strong>520 tests en verde</strong> en catorce proyectos,",
        "<p><strong>524 tests en verde</strong> en catorce proyectos,",
    ),
    (
        '<tr><td class="mono">ADR 0001 – 0016</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos, los límites por usuario, la base que guarda la forma de las fuentes, el archivo que se reconoce por su encabezado y el filtro que vive en la URL y solo acota</td></tr>',
        '<tr><td class="mono">Notice · ApiProblem · notices.ts</td><td>Fase 11: el aviso como código y datos, el error de API con <code>type</code> estable y el único catálogo de frases del sitio, vigilado por una prueba</td></tr>\n'
        '        <tr><td class="mono">ADR 0001 – 0017</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos, los límites por usuario, la base que guarda la forma de las fuentes, el archivo que se reconoce por su encabezado, el filtro que vive en la URL y solo acota, y el backend que no redacta frases</td></tr>',
    ),
    ("  <h3>Estructura del repositorio</h3>", SECTION + "  <h3>Estructura del repositorio</h3>"),
]

for old, new in pairs:
    count = s.count(old)
    assert count == 1, f"{count} coincidencias: {old[:70]!r}"
    s = s.replace(old, new)

if crlf:
    s = s.replace("\n", "\r\n")

io.open(PATH, "w", encoding="utf-8", newline="").write(s)
print("ok artefacto")
