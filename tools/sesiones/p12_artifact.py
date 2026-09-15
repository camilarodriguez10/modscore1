"""Artefacto del plan: cierre de la Fase 12."""
import io

PATH = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"

raw = io.open(PATH, encoding="utf-8", newline="").read()
crlf = "\r\n" in raw
s = raw.replace("\r\n", "\n")

SECTION = """  <h3>Fase 12 — administración de reglas</h3>
  <div class="ship">
    <div class="h">Criterio de aceptación cumplido, en vivo y por prueba</div>
    <p><strong>Cambiar un peso desde el portal mueve el score del portafolio, queda auditado con su impacto y no requiere recompilar ni desplegar.</strong> En vivo, sobre 250 aplicaciones, se pasaron tres puntos de peso de Kenna a MFA. La simulación mostró el efecto antes de publicar: el promedio de 70,52 a 70,68, con 101 aplicaciones que suben y 65 que bajan. Al publicar, v1.4 quedó vigente en el mismo proceso. Restaurar v1.3 publicó v1.5, y el promedio volvió a 70,52.</p>
  </div>

  <div class="prose">
    <p>Las reglas dejan de ser un recurso embebido y pasan a ser <strong>versiones publicadas</strong>. Ruleset, palancas y roadmap se publican juntos porque dependen entre sí: los objetivos de las palancas salen de las bandas del ruleset, y la proyección de cada roadmap sale de sus palancas. Nada se edita en su lugar: volver atrás es publicar otra vez lo que había.</p>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:36%">Se edita desde el portal</th><th>Queda en el código, y por qué</th></tr></thead>
      <tbody>
        <tr><td>Pesos: el total da 100 y cada pilar suma sus métricas</td><td>Qué métricas existen y de dónde leen su dato: lo decide un extractor</td></tr>
        <tr><td>Bandas, y puntajes de casos y de categorías</td><td>Las claves de casos y categorías: una nueva nunca coincidiría con el dato</td></tr>
        <tr><td>Qué palancas ofrece el laboratorio y su cantidad por defecto</td><td>La transformación de cada palanca, que describe una acción real</td></tr>
        <tr><td>Umbrales del roadmap, palancas por roadmap y semanas por hito</td><td>La normalización 20–100: tramos, score objetivo y umbrales usan esa escala</td></tr>
      </tbody>
    </table>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:28%">Control</th><th>Cómo quedó</th></tr></thead>
      <tbody>
        <tr><td><strong>Rol propio</strong></td><td><code>ruleset.admin</code>, separado de <code>ingestion.write</code>, con alcance completo: quien sube los datos no decide cómo se puntúan</td></tr>
        <tr><td><strong>Reautenticación</strong></td><td>Publicar exige una autenticación de los últimos 10 minutos. Con OIDC se pide <code>max_age=0</code> y solo cuenta un <code>auth_time</code> reciente. ASVS 3.7.1 pasa a cumple</td></tr>
        <tr><td><strong>Defensa en profundidad</strong></td><td>La capa System vuelve a exigir alcance completo y los permisos reenviados, y solo los acepta de un llamador con token de servicio</td></tr>
        <tr><td><strong>Concurrencia optimista</strong></td><td>Un borrador preparado sobre una versión que ya no es la vigente se rechaza, y el almacén repite la comprobación al escribir</td></tr>
        <tr><td><strong>Entrada</strong></td><td>Cuerpo de 128 KB, JSON estricto, identificadores por lista blanca y sin eco de lo recibido</td></tr>
        <tr><td><strong>Auditoría</strong></td><td>Cada versión guarda quién la publicó, por qué, qué cambió y su impacto. Eventos 4211, 4212, 4004 y 4104</td></tr>
      </tbody>
    </table>
  </div>

  <div class="note"><strong>Una corrección al plan.</strong> Se había dicho que cada carga conservaría la versión de reglas con que se calculó, pero eso mezclaría en una misma tendencia un cambio de datos con uno de reglas. La historia se puntúa con las reglas vigentes; lo que no se reescribe es el registro de cada versión. <strong>598 pruebas en verde</strong>, cero paquetes vulnerables en quince proyectos y <code>npm audit</code> limpio. La reautenticación contra un IdP real queda por verificar en el primer entorno que lo tenga.</div>

"""

pairs = [
    (
        "<span>Fases 00–11 · <b>entregadas — 524 tests en verde</b></span>",
        "<span>Fases 00–12 · <b>entregadas — 598 tests en verde</b></span>",
    ),
    (
        "<strong>100 cumple</strong>; 15 parciales, verificables solo en un despliegue real —mTLS, TLS del ingress, gestor de secretos, SIEM—; 3 pendientes, de severidad baja o de infraestructura; y 12 a cargo del IdP o sin funcionalidad a la que aplicar.",
        "<strong>103 cumple</strong>; 14 parciales, verificables solo en un despliegue real —mTLS, TLS del ingress, gestor de secretos, SIEM—; 3 pendientes, de severidad baja o de infraestructura; y 10 a cargo del IdP o sin funcionalidad a la que aplicar.",
    ),
    (
        '<tr><td class="mono">12</td><td>Administración de reglas</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>Editar ruleset, palancas y roadmap desde el portal, solo administradores, con simulación de impacto</td></tr>',
        '<tr class="shipped"><td class="mono">12</td><td>Administración de reglas</td><td><span class="done">ENTREGADA</span></td><td><strong>Un peso cambiado desde el portal mueve el portafolio, queda auditado y lo toman las réplicas sin desplegar</strong> · borrador, simulación sobre todo el portafolio y versiones inmutables · reautenticación</td></tr>',
    ),
    ("<h3>Fases 00–11 — entregadas</h3>", "<h3>Fases 00–12 — entregadas</h3>"),
    (
        "<p><strong>524 tests en verde</strong> en catorce proyectos,",
        "<p><strong>598 tests en verde</strong> en quince proyectos,",
    ),
    (
        '<tr><td class="mono">ADR 0001 – 0017</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos, los límites por usuario, la base que guarda la forma de las fuentes, el archivo que se reconoce por su encabezado, el filtro que vive en la URL y solo acota, y el backend que no redacta frases</td></tr>',
        '<tr><td class="mono">Modernization.Rules · IRulesStore</td><td>Fase 12: el editor que valida un borrador con códigos, la simulación del impacto sobre todo el portafolio y las versiones de reglas en memoria, archivos o base de datos</td></tr>\n'
        '        <tr><td class="mono">ADR 0001 – 0018</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos, los límites por usuario, la base que guarda la forma de las fuentes, el archivo que se reconoce por su encabezado, el filtro que vive en la URL y solo acota, el backend que no redacta frases y las reglas que se publican desde el portal como versiones</td></tr>',
    ),
    (
        "La Fase 12 los hará editables desde el portal, así que no pueden vivir en el bundle",
        "Desde la Fase 12 viajan en las versiones de reglas que publica el portal, así que no pueden vivir en el bundle",
    ),
    ("  <h3>Estructura del repositorio</h3>", SECTION + "  <h3>Estructura del repositorio</h3>"),
]

for old, new in pairs:
    count = s.count(old)
    assert count == 1, (count, old[:80])
    s = s.replace(old, new)

if crlf:
    s = s.replace("\n", "\r\n")

io.open(PATH, "w", encoding="utf-8", newline="").write(s)
print("ok artefacto")
