import io
p = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"
s = io.open(p, encoding='utf-8').read()

def rep(old, new, n=1):
    global s
    assert old in s, f"NO MATCH: {old[:90]!r}"
    s = s.replace(old, new, n)

rep('<span>Fases 00–03 · <b>entregadas — 196 tests en verde</b></span>',
    '<span>Fases 00–04 · <b>entregadas — SPA funcionando sobre 196 tests en verde</b></span>')

rep('<tr><td class="mono">04</td><td>SPA base</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>La pantalla “Cómo se calculó” reproduce el layout del dashboard v1.3</td></tr>',
    '<tr class="shipped"><td class="mono">04</td><td>SPA base</td><td><span class="done">ENTREGADA</span></td><td>React 19 + Vite + Tailwind 4 · cuatro pantallas · <code>npm audit</code> en cero · paleta de datos validada</td></tr>')

rep('  <h3>Fases 00–03 — entregadas</h3>', '  <h3>Fases 00–04 — entregadas</h3>')

rep('''    <p>El 14381 se reproduce <strong>extremo a extremo</strong>: generador → puerto <code>ISourceReader</code> → ensamblado → motor → <code>system-api</code> → <code>channel-api</code> → respuesta autenticada del BFF. Cada eslabón de esa cadena está cubierto por tests.</p>''',
    '''    <p>El 14381 se reproduce <strong>extremo a extremo</strong>: generador → puerto <code>ISourceReader</code> → ensamblado → motor → <code>system-api</code> → <code>channel-api</code> → SPA. Cada eslabón de esa cadena está cubierto por tests, y el último se ve en pantalla.</p>''')

rep('        <tr><td class="mono">ADR 0001 – 0005</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, y BFF con token de servicio</td></tr>',
    '''        <tr><td class="mono">src/web</td><td>React 19 + Vite 7 + TypeScript estricto + Tailwind 4 · React Router 7 · TanStack Query 5 · cliente sin token en memoria, solo cookie de sesión</td></tr>
        <tr><td class="mono">Pantallas 01 · 02 · 03 · 07</td><td>Portafolio con filtros y agregados, resumen del score con las tarjetas de pilar del dashboard v1.3, <strong>cómo se calculó</strong>, y linaje de fuentes</td></tr>
        <tr><td class="mono">Paleta de datos</td><td>3 hues categóricos para los pilares, validados contra las superficies reales de la app: PASS en los cinco gates, en claro y en oscuro, con la lista de pares completa</td></tr>
        <tr><td class="mono">ADR 0001 – 0006</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, BFF con token de servicio, y SPA sin librería de gráficos</td></tr>''')

# Nueva sección con lo que la pantalla de explicabilidad muestra
anchor = '  <h4>Superficie de API entregada</h4>'
screen = '''  <h4>La pantalla que responde el requisito 4</h4>
  <div class="prose">
    <p>“Cómo se calculó” es la razón por la que el motor produce un desglose completo en lugar de solo un número. Todo lo que aparece ahí se puede reproducir a mano con la fórmula del BRD.</p>
  </div>
  <div class="tw">
    <table>
      <thead><tr><th style="width:30%">Bloque</th><th>Qué muestra</th></tr></thead>
      <tbody>
        <tr><td>La fórmula</td><td>La expresión del BRD con los números de esta aplicación ya sustituidos, más los cinco pasos intermedios: numerador, denominador, métricas excluidas, cociente y normalización</td></tr>
        <tr><td>Composición del numerador</td><td>Una barra donde cada segmento es una métrica, coloreado por pilar. El ancho total es el peso disponible, así que <strong>el hueco al final es exactamente lo que la aplicación no ganó</strong></td></tr>
        <tr><td>Peso que entra al cálculo</td><td>60 de 100 en el caso de referencia. Un score alto con poco peso detrás dice menos que uno igual con el peso completo</td></tr>
        <tr><td>Aporte por métrica</td><td>Ordenado de mayor a menor, con el ponderado y cuántos puntos del score final aporta cada uno</td></tr>
        <tr><td>Métricas incluidas</td><td>Valor crudo → regla aplicada → puntaje → peso → ponderado → procedencia (sistema, reporte, campo y adaptador que lo entregó)</td></tr>
        <tr><td>Métricas excluidas</td><td>Cada una con su peso perdido y el motivo textual: “El scan SCA no reportó severidad Critical”</td></tr>
      </tbody>
    </table>
  </div>

''' + anchor
rep(anchor, screen)

# Decisiones de fase 4
rep('    <li><strong>Los agregados del portafolio cubren el conjunto filtrado, no la página.</strong> Calcularlos sobre la página daría un promedio distinto en cada scroll — un bug sutil que solo aparece al paginar.</li>',
    '''    <li><strong>Los agregados del portafolio cubren el conjunto filtrado, no la página.</strong> Calcularlos sobre la página daría un promedio distinto en cada scroll — un bug sutil que solo aparece al paginar.</li>
    <li><strong>El backend omitía las propiedades nulas y eso reventaba el front.</strong> Con <code>WhenWritingNull</code>, el cliente recibía <code>undefined</code> donde su tipo decía <code>null</code>, y una comprobación <code>=== null</code> dejaba la página en blanco. El modelo de vista es el contrato del front: ahora los nulos se serializan explícitos, el cliente usa <code>== null</code>, y un error boundary convierte cualquier crash de render en un mensaje con salida.</li>
    <li><strong>Un puntaje de 97,5 no puede mostrarse como 98.</strong> Contradecía la regla que aparecía a su lado en la misma fila. Los decimales se muestran solo cuando existen.</li>
    <li><strong>Sin librería de gráficos.</strong> Las tres visualizaciones que la app necesita no tienen ejes ni escalas continuas. Se dibujan con HTML y CSS: una dependencia menos, y control total sobre los huecos de 2 px entre segmentos y el tramo “sin ganar”.</li>
    <li><strong>La cifra grande va en tinta, no en color de estado.</strong> En modo claro dos de los cuatro tramos quedan bajo 3:1 contra la superficie: un 79,67 de 64 px en amarillo sería el peor lugar posible para gastar un color de bajo contraste. El chip de al lado carga el color.</li>''')

io.open(p, 'w', encoding='utf-8').write(s)
print("ok")
