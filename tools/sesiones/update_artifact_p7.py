import io
p = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"
s = io.open(p, encoding='utf-8').read()

def rep(old, new, n=1):
    global s
    assert old in s, f"NO MATCH: {old[:100]!r}"
    s = s.replace(old, new, n)

# ---------------------------------------------------------------- cabecera
rep('<span>Fases 00–06 · <b>entregadas — 274 tests en verde</b></span>',
    '<span>Fases 00–07 · <b>entregadas — 343 tests en verde</b></span>')

# ---------------------------------------------------------------- plan de fases
rep('<tr><td class="mono">07</td><td>Adaptador de archivos</td><td><div class="dur"><i style="width:16px"></i><span>1 sem</span></div></td><td>Mismo score leyendo dummy que leyendo sus CSV exportados</td></tr>',
    '<tr class="shipped"><td class="mono">07</td><td>Adaptador de archivos</td><td><span class="done">ENTREGADA</span></td><td><strong>Mismo score leyendo dummy que leyendo sus CSV exportados</strong> · módulo de carga en la UI · data histórica por versiones</td></tr>')

rep('<div class="note">Las fases 07 y 09 son deliberadamente cortas: el trabajo de aislamiento se hace en la fase 02, no cuando llegan los archivos reales.</div>',
    '<div class="note">La fase 09 será corta por la misma razón que lo fue la 07: el trabajo de aislamiento se hizo en la fase 02, no cuando llegaron los archivos reales.</div>')

rep('  <h3>Fases 00–06 — entregadas</h3>', '  <h3>Fases 00–07 — entregadas</h3>')

rep('<p><strong>274 tests en verde</strong> en doce proyectos, cero paquetes vulnerables.',
    '<p><strong>343 tests en verde</strong> en doce proyectos, cero paquetes vulnerables.')

# Bloque de aceptación obsoleto de una fase anterior: contradice al de arriba.
rep('''  <div class="ship">
    <div class="h">Criterio de aceptación cumplido</div>
    <p><strong>102 tests en verde.</strong> El motor reproduce exactamente los dos casos documentados: <span class="mono">EAI 14381 = 79,67</span> derivado de datos crudos por el pipeline completo, y <span class="mono">EAI 10969 = 79,61</span> contra el dashboard v1.3, métrica por métrica.</p>
    <p>Compila con <code>TreatWarningsAsErrors</code> y analizadores en nivel recomendado. <code>dotnet list package --vulnerable --include-transitive</code> reporta cero paquetes vulnerables en los tres proyectos.</p>
  </div>

''', '')

# ---------------------------------------------------------------- entregables
rep('        <tr><td class="mono">ADR 0001 – 0008</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, BFF con token de servicio, SPA sin librería de gráficos, el what-if que recalcula en lugar de sumar, y el roadmap que proyecta con ese mismo motor</td></tr>',
    '''        <tr><td class="mono">Modernization.Sources.Files</td><td>Lector CSV validado contra contratos de columna, adaptador de archivos con versiones, y servicio de carga con staging y publicación atómica</td></tr>
        <tr><td class="mono">contracts/sources/source-schemas.json</td><td>Las 8 fuentes con sus columnas, tipos, alias y clave — el mismo contrato que valida la carga y guía la lectura</td></tr>
        <tr><td class="mono">Pantalla 08 · Cargas</td><td>Subir los archivos y recalcular, veredicto archivo por archivo con filas leídas y rechazadas, historial de versiones y evolución del score del portafolio</td></tr>
        <tr><td class="mono">ADR 0001 – 0010</td><td>A los ocho anteriores se suman la historia como dimensión del portafolio y la carga de archivos como superficie de ataque</td></tr>''')

# ---------------------------------------------------------------- API entregada
rep('        <tr><td>Channel</td><td class="mono">GET /api/session · POST /api/logout</td><td>Identidad, token CSRF y cierre de sesión del lado servidor</td></tr>',
    '''        <tr><td>Channel</td><td class="mono">GET /api/ingestion</td><td>Pantalla 08: versiones publicadas, archivos esperados con su contrato, y la serie del portafolio</td></tr>
        <tr><td>Channel</td><td class="mono">POST /api/ingestion/upload</td><td>Publica una carga nueva y devuelve el veredicto por archivo. Requiere sesión y token CSRF</td></tr>
        <tr><td>Channel</td><td class="mono">GET /api/applications/{eai}/history</td><td>Evolución del score de una aplicación, con el peso disponible en cada punto</td></tr>
        <tr><td>Channel</td><td class="mono">GET /api/session · POST /api/logout</td><td>Identidad, token CSRF y cierre de sesión del lado servidor</td></tr>''')

# ---------------------------------------------------------------- data histórica (fin de la sección 02)
anchor = '<!-- 3 -->'
history = '''  <h3>Data histórica: primero archivos, después base de datos</h3>
  <div class="prose">
    <p>Un score aislado dice poco. La pregunta que hace un comité de portafolio no es <em>“¿cuánto vale esta aplicación?”</em> sino <em>“¿está mejorando?”</em>. Por eso la versión no es una tabla lateral: <strong>es parte del puerto de fuentes</strong>, y los tres adaptadores la resuelven cada uno a su manera sin que nada aguas arriba lo note.</p>
  </div>

  <div class="formula">
    <div class="eq">Task&lt;IReadOnlyList&lt;SnapshotVersion&gt;&gt; GetVersionsAsync(...)</div>
    <div class="def">
      <div><b>GetAllAsync(versionId, ...)</b>&nbsp; el portafolio tal como estaba en esa versión</div>
      <div>Un adaptador sin historia devuelve una sola versión y responde igual. El motor, el what-if y el roadmap no distinguen los dos casos.</div>
    </div>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:22%">Aspecto</th><th style="width:39%">Fase de archivos <span class="done">ENTREGADA</span></th><th>Fase de base de datos <em>(Fase 09)</em></th></tr></thead>
      <tbody>
        <tr><td>Qué es una versión</td><td>Un directorio con los CSV de esa carga y su <code>manifest.json</code></td><td>Una fila de <code>snapshot_version</code>; cada tabla de hechos gana <code>version_id</code> en su clave</td></tr>
        <tr><td>Publicar</td><td>Staging → validación → ensamblado → <code>Directory.Move</code> atómico</td><td>Una transacción por carga</td></tr>
        <tr><td>Consulta histórica</td><td>Relee el directorio de esa versión</td><td><code>WHERE version_id = @v</code>, con índice</td></tr>
        <tr><td>Coste de la serie</td><td>Recorre todas las versiones — aceptable con pocas cargas</td><td>Tabla <code>score_snapshot</code> precalculada</td></tr>
        <tr><td>Retención</td><td>Manual: el directorio es la unidad de borrado</td><td>Política declarativa: N versiones o M meses</td></tr>
        <tr><td>Puerto <code>ISourceReader</code></td><td colspan="2"><strong>No cambia.</strong> Es el punto de todo el diseño: el motor, el what-if, el roadmap y las ocho pantallas siguen exactamente igual</td></tr>
      </tbody>
    </table>
  </div>

  <div class="note"><strong>El score se muestra junto al peso disponible en cada punto.</strong> Un salto con el mismo peso detrás es mejora real; con más peso puede ser solo más evidencia. La gráfica de evolución lleva ambos, porque sin el segundo el primero engaña.</div>
</section>

<hr class="div">

''' + anchor
rep('</section>\n\n<hr class="div">\n\n<!-- 3 -->', '\n' + history)

# ---------------------------------------------------------------- carga de archivos (fin de la sección 07)
rep('  <div class="note">Objetivo de conformidad: <strong>OWASP ASVS Nivel 2</strong>, con verificación explícita del Top 10.</div>',
    '''  <div class="note">Objetivo de conformidad: <strong>OWASP ASVS Nivel 2</strong>, con verificación explícita del Top 10.</div>

  <h3>El módulo de carga y su superficie de ataque</h3>
  <div class="prose">
    <p>Subir archivos es el endpoint más peligroso de cualquier aplicación, y aquí además <strong>reemplaza el portafolio completo</strong>: un abuso no sería solo una intrusión, sería corromper la base sobre la que se deciden inversiones. Cada control de esta tabla tiene su test.</p>
  </div>
  <div class="tw">
    <table>
      <thead><tr><th style="width:34%">Control</th><th style="width:22%">Dónde</th><th>Qué impide</th></tr></thead>
      <tbody>
        <tr><td>Autenticación + CSRF de doble envío</td><td class="mono">channel-api</td><td>Que un formulario alojado en otro sitio reemplace el portafolio de la organización</td></tr>
        <tr><td>Límite de tamaño de cuerpo</td><td class="mono">ambas capas</td><td>Agotamiento de memoria y disco — se aplica antes de leer un byte</td></tr>
        <tr><td>Extensión y content-type en lista blanca</td><td class="mono">IngestionService</td><td>Ejecutables y contenido activo disfrazados de datos</td></tr>
        <tr><td>Tope de archivos por carga</td><td class="mono">IngestionService</td><td>Que una petición con cientos de partes agote el proceso</td></tr>
        <tr><td><strong>El nombre del cliente nunca es una ruta</strong></td><td class="mono">UploadService</td><td>Path traversal: <code>../../appsettings.json</code> no puede tocar nada</td></tr>
        <tr><td>Lista blanca de nombres esperados</td><td class="mono">UploadService</td><td>Archivos que no corresponden a ninguna fuente conocida</td></tr>
        <tr><td>Validación de esquema antes de aceptar</td><td class="mono">CsvSourceReader</td><td>Datos que producirían scores incorrectos sin que nadie lo note</td></tr>
        <tr><td>Staging + publicación atómica</td><td class="mono">UploadService</td><td>Que una carga a medias deje el portafolio inconsistente</td></tr>
        <tr><td>Rechazo de la carga vacía</td><td class="mono">UploadService</td><td>Reemplazar 250 aplicaciones por ninguna — peor que no cargar nada</td></tr>
      </tbody>
    </table>
  </div>
  <div class="note">Lo que se escribe en disco es <strong>el nombre canónico del contrato</strong>, nunca el que envió el cliente: el nombre entrante solo sirve para emparejar contra la lista blanca, y de ahí no pasa.</div>''')

# ---------------------------------------------------------------- decisiones
rep('    <li><strong>Agrupar por disposition, no una matriz 3×7.</strong> MAPS solo define siete pares válidos, así que una matriz tendría catorce celdas vacías. La estructura de la pantalla refleja la del modelo.</li>',
    '''    <li><strong>Agrupar por disposition, no una matriz 3×7.</strong> MAPS solo define siete pares válidos, así que una matriz tendría catorce celdas vacías. La estructura de la pantalla refleja la del modelo.</li>
    <li><strong>Dos niveles de fallo, deliberadamente distintos.</strong> Una columna obligatoria que falta invalida el archivo entero: seguir leyendo produciría un portafolio incompleto sin que nadie lo note. Una fila mal formada se rechaza con su motivo y la lectura continúa: un dato malo en la fila 300 no puede dejar sin score a las otras 249.</li>
    <li><strong>Los identificadores de carga llevan sufijo aleatorio.</strong> Con granularidad de segundos, dos cargas en el mismo segundo generaban el mismo id y la segunda borraba a la primera en silencio. Lo detectó un test antes de que llegara a ningún lado.</li>
    <li><strong>Los alias de columna vienen del mundo real.</strong> Power BI exporta <code>Suma de Invest_Count</code> donde el sistema origen escribe <code>Invest_Count</code>, y los archivos llegan con prefijo de fecha. El contrato acepta ambas formas y empareja por la raíz del nombre, así <code>0809 tech_debt.csv</code> entra sin renombrar nada.</li>
    <li><strong>Solo CSV.</strong> Los exports observados son CSV — las capturas muestran el aviso de Excel al guardarlos. Aceptar XLSX añadiría una librería de parseo y una superficie de ataque nueva a cambio de comodidad; queda fuera hasta que haya un caso concreto.</li>
    <li><strong>Validar la entrada antes de mirar si la función está habilitada.</strong> Al revés, un archivo malicioso enviado contra un adaptador que no admite cargas se iba sin ser examinado, y el mismo archivo pasaría el día que sí la admitiera. El orden de las guardas es parte del control.</li>''')

# ---------------------------------------------------------------- estructura
rep('├─ data/dummy/                     <em>CSV generados, listos para la Fase 2</em>',
    '''├─ data/dummy/                     <em>CSV generados, listos para la Fase 2</em>
├─ <b>data/history/&lt;id&gt;/</b>              <em>una carga publicada = una versión ✓</em>''')

rep('│  │  └─ Modernization.Sources/{<b>Dummy</b>,File,Database}/',
    '│  │  └─ Modernization.Sources/{<b>Dummy</b>,<b>Files</b>,Database}/')

rep('│  ├─ SourceReader.ContractTests/  <em>una suite, tres adaptadores</em>',
    '│  ├─ SourceReader.ContractTests/  <em>una suite, dos adaptadores ✓</em>')

io.open(p, 'w', encoding='utf-8').write(s)
print("ok")
