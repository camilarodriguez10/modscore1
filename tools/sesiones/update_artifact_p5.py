import io
p = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"
s = io.open(p, encoding='utf-8').read()

def rep(old, new, n=1):
    global s
    assert old in s, f"NO MATCH: {old[:90]!r}"
    s = s.replace(old, new, n)

rep('<span>Fases 00–04 · <b>entregadas — SPA funcionando sobre 196 tests en verde</b></span>',
    '<span>Fases 00–05 · <b>entregadas — What-If Lab sobre 237 tests en verde</b></span>')

rep('<tr><td class="mono">05</td><td>What-If</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>Δ MFA = +5,60 pts verificado contra el cálculo manual</td></tr>',
    '<tr class="shipped"><td class="mono">05</td><td>What-If</td><td><span class="done">ENTREGADA</span></td><td><strong>Δ MFA = +5,60 pts verificado contra el cálculo manual</strong> · 13 palancas · escenarios compuestos · objetivo inverso · techo alcanzable</td></tr>')

rep('  <h3>Fases 00–04 — entregadas</h3>', '  <h3>Fases 00–05 — entregadas</h3>')

rep('''    <p><strong>196 tests en verde</strong> en diez proyectos, cero paquetes vulnerables.''',
    '''    <p><strong>237 tests en verde</strong> en once proyectos, cero paquetes vulnerables.''')

rep('        <tr><td class="mono">ADR 0001 – 0006</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, BFF con token de servicio, y SPA sin librería de gráficos</td></tr>',
    '''        <tr><td class="mono">Modernization.WhatIf</td><td>13 palancas expresadas como transformaciones puras del snapshot, motor de escenarios compuestos, objetivo inverso con poda, y techo alcanzable</td></tr>
        <tr><td class="mono">Pantalla 04 · What-If Lab</td><td>Palancas con su ganancia y la acción real que representan, recálculo en vivo, comparación base → escenario → techo, y búsqueda por score objetivo</td></tr>
        <tr><td class="mono">ADR 0001 – 0007</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, BFF con token de servicio, SPA sin librería de gráficos, y el what-if que recalcula en lugar de sumar</td></tr>''')

# Reemplazar la tabla de palancas teórica por la verificada
old_table_intro = '''  <h3>Catálogo de palancas</h3>
  <div class="prose"><p>Ganancia calculada sobre un denominador de 80 — el caso de EAI 10969. La barra ordena las palancas por retorno.</p></div>'''
new_table_intro = '''  <h3>Catálogo de palancas</h3>
  <div class="prose"><p>Ganancia sobre un denominador de 80. Cada fila está verificada por un test que la comprueba contra la fórmula además de contra el motor.</p></div>'''
rep(old_table_intro, new_table_intro)

# Nueva sección: el hallazgo del what-if
anchor = '  <h3>Capacidades del servicio</h3>'
finding = '''  <h3>Por qué el motor recalcula en lugar de sumar</h3>
  <div class="prose">
    <p>La forma barata de simular sería calcular el delta de cada palanca una vez y sumarlos. Con esta fórmula, eso da resultados incorrectos — y el error va en la dirección optimista, que es la peor.</p>
  </div>

  <div class="verify">
    <div class="verify-hd">
      <span class="t">Medido · EAI 14381 con tres palancas que aportan dato nuevo</span>
      <span class="s">error del 25 %</span>
    </div>
    <div class="tw">
      <table>
        <thead><tr><th>Cálculo</th><th class="num">Peso disponible</th><th class="num">Score</th><th class="num">Δ</th></tr></thead>
        <tbody>
          <tr><td>Línea base</td><td class="num">60</td><td class="num">79,67</td><td class="num">—</td></tr>
          <tr><td>Sumando los deltas individuales</td><td class="num">60</td><td class="num">96,13</td><td class="num" style="color:var(--crit)">+16,46</td></tr>
          <tr class="total"><td>Recalculando desde cero</td><td class="num">80</td><td class="num">92,75</td><td class="num match">+13,08</td></tr>
        </tbody>
      </table>
    </div>
  </div>

  <div class="note">La razón está en el denominador: una palanca que aporta dato a una métrica que no lo tenía cambia las <strong>dos</strong> partes de la división. El delta de una palanca depende entonces de qué otras se apliquen con ella. La app muestra las tres cifras juntas y explica la diferencia en texto, en vez de dejar al usuario sospechando del cálculo.</div>

''' + anchor
rep(anchor, finding)

# Decisiones de fase 5
rep('    <li><strong>La cifra grande va en tinta, no en color de estado.</strong> En modo claro dos de los cuatro tramos quedan bajo 3:1 contra la superficie: un 79,67 de 64 px en amarillo sería el peor lugar posible para gastar un color de bajo contraste. El chip de al lado carga el color.</li>',
    '''    <li><strong>La cifra grande va en tinta, no en color de estado.</strong> En modo claro dos de los cuatro tramos quedan bajo 3:1 contra la superficie: un 79,67 de 64 px en amarillo sería el peor lugar posible para gastar un color de bajo contraste. El chip de al lado carga el color.</li>
    <li><strong>Cada palanca es una acción, no un movimiento del número.</strong> “Integrar la aplicación al proveedor corporativo de multifactor”, no “subir la métrica MFA”. Un catálogo que optimiza el número en vez del portafolio es peor que no tenerlo.</li>
    <li><strong>El techo no siempre es 100.</strong> Cuando la fuente entrega el compuesto ya calculado en lugar del detalle por producto, no hay conteos que reclasificar y ninguna palanca puede moverlo. Se reporta el motivo — y es también un argumento para pedirle a CMT el detalle.</li>
    <li><strong>Simular no puede ser destructivo.</strong> Los escenarios operan sobre copias del snapshot, y hay un test end-to-end que verifica que el score real no cambia después de simular.</li>''')

io.open(p, 'w', encoding='utf-8').write(s)
print("ok")
