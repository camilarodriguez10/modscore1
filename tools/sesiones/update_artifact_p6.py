import io
p = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"
s = io.open(p, encoding='utf-8').read()

def rep(old, new, n=1):
    global s
    assert old in s, f"NO MATCH: {old[:90]!r}"
    s = s.replace(old, new, n)

rep('<span>Fases 00–05 · <b>entregadas — What-If Lab sobre 237 tests en verde</b></span>',
    '<span>Fases 00–06 · <b>entregadas — 274 tests en verde</b></span>')

rep('<tr><td class="mono">06</td><td>Roadmap</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>Las siete combinaciones disposition/roadmap modeladas</td></tr>',
    '<tr class="shipped"><td class="mono">06</td><td>Roadmap</td><td><span class="done">ENTREGADA</span></td><td><strong>Las 7 combinaciones disposition/roadmap modeladas</strong> · hitos con fechas y owners · proyección desde el motor de what-if · 8 reglas de coherencia</td></tr>')

rep('  <h3>Fases 00–05 — entregadas</h3>', '  <h3>Fases 00–06 — entregadas</h3>')

rep('''    <p><strong>237 tests en verde</strong> en once proyectos, cero paquetes vulnerables.''',
    '''    <p><strong>274 tests en verde</strong> en doce proyectos, cero paquetes vulnerables.''')

rep('        <tr><td class="mono">ADR 0001 – 0007</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, BFF con token de servicio, SPA sin librería de gráficos, y el what-if que recalcula en lugar de sumar</td></tr>',
    '''        <tr><td class="mono">Modernization.Roadmap</td><td>Modelo MAPS como contrato versionado: 3 dispositions, 7 roadmaps con sus hitos y owners, 8 reglas de coherencia, y el planificador que proyecta el score futuro</td></tr>
        <tr><td class="mono">Pantallas 05 · 06</td><td>Plan futuro por aplicación con timeline y alertas, y el portafolio agrupado por disposition con lo que gana cada camino</td></tr>
        <tr><td class="mono">ADR 0001 – 0008</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, BFF con token de servicio, SPA sin librería de gráficos, el what-if que recalcula en lugar de sumar, y el roadmap que proyecta con ese mismo motor</td></tr>''')

# Sección nueva: los números del portafolio por roadmap
anchor = '  <h3>Vista “plan futuro” por aplicación</h3>'
numbers = '''  <h3>Lo que gana cada camino, medido</h3>
  <div class="prose">
    <p>Cada roadmap declara qué palancas del what-if entrega su ejecución, con la justificación de por qué esas. El score proyectado sale de aplicarlas al mismo motor que calcula el actual — no de una tabla escrita a mano que se desincronizaría al primer cambio de peso.</p>
  </div>
  <div class="tw">
    <table>
      <thead><tr><th style="width:14%">Disposition</th><th style="width:20%">Roadmap</th><th class="num">Apps</th><th class="num">Hoy</th><th class="num">Al terminar</th><th class="num">Δ</th><th class="num">Duración</th></tr></thead>
      <tbody>
        <tr><td rowspan="3">Invest</td><td>Refactor</td><td class="num">37</td><td class="num">67,94</td><td class="num">82,15</td><td class="num" style="color:var(--good)">+14,21</td><td class="num">42 sem</td></tr>
        <tr><td>Maintain (Evolve)</td><td class="num">33</td><td class="num">73,07</td><td class="num">89,52</td><td class="num" style="color:var(--good)">+16,45</td><td class="num">28 sem</td></tr>
        <tr><td>Reimagine</td><td class="num">32</td><td class="num">70,57</td><td class="num">96,26</td><td class="num" style="color:var(--good)">+25,69</td><td class="num">60 sem</td></tr>
        <tr><td rowspan="2">Tolerate</td><td>Maintain (Patch)</td><td class="num">35</td><td class="num">67,65</td><td class="num">82,78</td><td class="num" style="color:var(--good)">+15,13</td><td class="num">14 sem</td></tr>
        <tr><td>Rehost</td><td class="num">35</td><td class="num">72,09</td><td class="num">74,71</td><td class="num" style="color:var(--good)">+2,62</td><td class="num">17 sem</td></tr>
        <tr><td rowspan="2">Eliminate</td><td>Consolidate</td><td class="num">41</td><td class="num">70,37</td><td class="num">—</td><td class="num">sale</td><td class="num">34 sem</td></tr>
        <tr><td>Retire</td><td class="num">37</td><td class="num">72,18</td><td class="num">—</td><td class="num">sale</td><td class="num">18 sem</td></tr>
      </tbody>
    </table>
  </div>
  <div class="note">Dos lecturas que solo aparecen al poner las columnas juntas. <strong>Maintain (Patch) es el mejor retorno del portafolio</strong>: +15,13 puntos en 14 semanas. <strong>Reimagine cuesta cuatro veces más tiempo por menos del doble de ganancia</strong> — sigue pudiendo ser la decisión correcta, pero por razones funcionales, no por el score. Y <strong>Rehost apenas mueve la aguja</strong> (+2,62), coherente con el BRD: sin recompilación, la arquitectura y la deuda quedan igual.</div>

''' + anchor
rep(anchor, numbers)

# Alertas de coherencia con números reales
old_alert_li = '    <li><strong>Alertas de coherencia</strong> — cuando disposition y datos se contradicen: una app marcada <em>Retire</em> con conteo alto de productos Invest, o <em>Tolerate</em> con SCA Critical abiertos.</li>'
new_alert_li = '''    <li><strong>Alertas de coherencia</strong> — ocho reglas que contrastan el plan declarado contra los datos. En el portafolio dummy disparan 196 veces: 35 aplicaciones <em>Tolerate</em> con vulnerabilidades críticas abiertas, 14 aplicaciones críticas fuera del plan de DR, 67 marcadas para salir del portafolio pero con productos en Invest. Cada alerta lleva la evidencia que la sostiene y enlaza a las aplicaciones concretas.</li>'''
rep(old_alert_li, new_alert_li)

# Decisiones de fase 6
rep('    <li><strong>Simular no puede ser destructivo.</strong> Los escenarios operan sobre copias del snapshot, y hay un test end-to-end que verifica que el score real no cambia después de simular.</li>',
    '''    <li><strong>Simular no puede ser destructivo.</strong> Los escenarios operan sobre copias del snapshot, y hay un test end-to-end que verifica que el score real no cambia después de simular.</li>
    <li><strong>El score futuro no se escribe a mano.</strong> Una tabla que dijera “Refactor sube 15 puntos” se desincronizaría del modelo la primera vez que alguien cambie un peso, y nadie se enteraría. Cada roadmap declara sus palancas y el número sale del motor.</li>
    <li><strong>Retire y Consolidate no proyectan mejora.</strong> Su score no sube: deja de existir. Proyectar una ganancia ahí justificaría invertir en algo que se va a apagar, y la interfaz lo dice con esas palabras.</li>
    <li><strong>Agrupar por disposition, no una matriz 3×7.</strong> MAPS solo define siete pares válidos, así que una matriz tendría catorce celdas vacías. La estructura de la pantalla refleja la del modelo.</li>''')

# RFC nuevo sobre el mapeo roadmap → palancas
rep('        <tr><td class="mono">RFC-08</td>',
    '''        <tr><td class="mono">RFC-09</td><td>Mapeo roadmap → palancas del what-if</td><td>El BRD define qué es cada roadmap pero no qué métricas mueve. El mapeo es una interpretación de esas definiciones, documentada en el contrato con su justificación. <strong>Conviene validarlo con los dueños del modelo.</strong></td></tr>
        <tr><td class="mono">RFC-08</td>''')

io.open(p, 'w', encoding='utf-8').write(s)
print("ok")
