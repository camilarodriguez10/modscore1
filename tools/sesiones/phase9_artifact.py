import io
import os

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, "mod-score-plan.html")

s = io.open(PATH, encoding="utf-8").read()


def rep(old, new):
    global s
    if old in s:
        s = s.replace(old, new, 1)
    elif new not in s:
        raise AssertionError(f"NO MATCH: {old[:100]!r}")


rep('<span>Fases 00–08 · <b>entregadas — 435 tests en verde</b></span>',
    '<span>Fases 00–09 · <b>entregadas — 466 tests en verde</b></span>')

rep('<tr><td class="mono">09</td><td>Adaptador de BD</td><td><div class="dur"><i style="width:16px"></i><span>1 sem</span></div></td><td>Contract tests verdes contra base de datos real</td></tr>',
    '<tr class="shipped"><td class="mono">09</td><td>Adaptador de BD</td><td><span class="done">ENTREGADA</span></td><td><strong>El golden 79,67 leído desde una base real</strong> · publicación transaccional · migraciones · retención declarativa</td></tr>\n'
    '        <tr><td class="mono">10</td><td>Experiencia de consulta</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>Filtros persistentes en todas las vistas, estadísticas clicables, roadmap por dimensión, linaje dentro de Cargas, nombres de archivo flexibles</td></tr>\n'
    '        <tr><td class="mono">11</td><td>Sitio en inglés</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>El backend deja de emitir texto para personas; la interfaz lo traduce</td></tr>\n'
    '        <tr><td class="mono">12</td><td>Administración de reglas</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>Editar ruleset, palancas y roadmap desde el portal, solo administradores, con simulación de impacto</td></tr>')

rep('<div class="note">La fase 09 será corta por la misma razón que lo fue la 07: el trabajo de aislamiento se hizo en la fase 02, no cuando llegaron los archivos reales.</div>',
    '<div class="note">La fase 09 fue corta por la misma razón que lo fue la 07: el trabajo de aislamiento se hizo en la fase 02, no cuando llegaron los archivos reales. Las fases 10 a 12 son la <strong>ampliación pedida el 12 de septiembre</strong>: experiencia de consulta, idioma y administración de reglas.</div>')

rep('  <h3>Fases 00–08 — entregadas</h3>', '  <h3>Fases 00–09 — entregadas</h3>')

rep('<p><strong>435 tests en verde</strong> en trece proyectos, cero paquetes vulnerables, y el build compila con todas las reglas de seguridad del analizador como errores.',
    '<p><strong>466 tests en verde</strong> en catorce proyectos, cero paquetes vulnerables —ya con EF Core y SQLite dentro—, y el build compila con todas las reglas de seguridad del analizador como errores.')

fase9 = '''
  <h3>Fase 09 — el tercer adaptador</h3>
  <div class="ship">
    <div class="h">Criterio de aceptación cumplido</div>
    <p><strong>La misma suite de contrato que verificó Dummy y File corre contra una base real</strong>, y el EAI de referencia sigue dando <span class="mono">79,67</span> leído desde ella. El proyecto de contratos pasó de 98 a 127 pruebas sin cambiar una línea del motor, del what-if, del roadmap ni de las ocho pantallas.</p>
  </div>

  <div class="prose">
    <p>La pregunta de diseño no era cómo hablar con una base, sino <strong>qué guardar en ella</strong>. La tentación es guardar el modelo canónico, que es lo que el motor consume. Sería un error: habría dos traducciones que mantener de acuerdo, y el mismo archivo podría puntuar distinto según por dónde entrara.</p>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:30%">Decisión</th><th>Por qué</th></tr></thead>
      <tbody>
        <tr><td><strong>Una tabla por fuente, con sus columnas</strong></td><td><code>EAI_CODE</code>, <code>APP_ID</code>, <code>"1-Critical"</code> tal como llegan. El ensamblador sigue siendo la única traducción del sistema</td></tr>
        <tr><td>La carga es una <strong>propiedad sombra</strong></td><td>Los registros de fuente son los mismos tipos que usan Dummy y File: ninguno lleva dentro un campo que solo existe por la persistencia</td></tr>
        <tr><td><strong>Publicar es una transacción</strong></td><td>El equivalente exacto del movimiento atómico del directorio: si algo falla a mitad, no queda una carga a medias</td></tr>
        <tr><td>Un <strong>puerto de publicación</strong> gemelo al de lectura</td><td>La capa System ya no conoce a ninguno de los dos adaptadores</td></tr>
        <tr><td>Los <strong>rechazos se guardan</strong></td><td>Con archivos se recalculan al leer; aquí el archivo ya no existe, y sin la tabla el linaje se apagaría en silencio</td></tr>
        <tr><td><strong>Retención declarativa</strong></td><td>Conserva las N cargas más recientes. Con archivos había que borrar directorios a mano</td></tr>
        <tr><td>EF Core <strong>no llega a la capa Channel</strong></td><td>Vive en su propio proyecto, con referencia privada: la Channel usa la System solo por sus contratos</td></tr>
      </tbody>
    </table>
  </div>

  <div class="note"><strong>Dos desviaciones deliberadas.</strong> Sin tabla de scores precalculados: la Fase 08 midió que cachear por versión dejó la historia en 0,006 s, y precalcular añadiría una copia que mantener coherente con el ruleset a cambio de milisegundos. Y el motor concreto sigue siendo RFC-07: el modelo es agnóstico y se verifica contra SQLite —una base real, con archivo, transacciones, índices y migraciones—; arrancar contra un motor no verificado falla explícitamente.</div>

  <h3>Estructura del repositorio</h3>'''

rep('  <h3>Estructura del repositorio</h3>', fase9)

rep('        <tr><td class="mono">ADR 0001 – 0013</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos y los límites por usuario</td></tr>',
    '''        <tr><td class="mono">Modernization.Sources.Database</td><td>Contexto EF Core con el esquema de las fuentes, proveedor, publicador transaccional, retención y migraciones</td></tr>
        <tr><td class="mono">ISnapshotPublisher</td><td>Puerto de publicación: el adaptador de archivos y el de base de datos lo implementan, y la capa System no distingue</td></tr>
        <tr><td class="mono">ADR 0001 – 0014</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos, los límites por usuario y la base que guarda la forma de las fuentes</td></tr>''')

rep('│  │  └─ Modernization.Sources/{<b>Dummy</b>,<b>Files</b>,Database}/',
    '│  │  ├─ Modernization.Sources/{<b>Dummy</b>,<b>Files</b>,Export}/  <em>✓</em>\n│  │  └─ <b>Modernization.Sources.Database/</b>     <em>EF Core, migraciones ✓</em>')

rep('<tr><td class="mono">RFC-07</td><td>Infraestructura</td><td>¿Entra ID corporativo o Keycloak self-hosted para el piloto? ¿AKS, coherente con las menciones a AKS/ASE del BRD?</td></tr>',
    '<tr><td class="mono">RFC-07</td><td>Infraestructura</td><td>¿Entra ID corporativo o Keycloak self-hosted para el piloto? ¿AKS, coherente con las menciones a AKS/ASE del BRD? Decide también el <strong>motor de base de datos</strong>: el adaptador es agnóstico y hoy se verifica contra SQLite.</td></tr>')

io.open(PATH, "w", encoding="utf-8").write(s)
print("ok artefacto; sections", s.count("<section"), "/", s.count("</section>"), "· tables", s.count("<table>"), "/", s.count("</table>"))
