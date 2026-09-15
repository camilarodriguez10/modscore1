import io
p = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"
s = io.open(p, encoding='utf-8').read()

def rep(old, new, n=1):
    global s
    assert old in s, f"NO MATCH: {old[:80]!r}"
    s = s.replace(old, new, n)

rep('<span>Fases 00–02 · <b>entregadas — 155 tests en verde</b></span>',
    '<span>Fases 00–03 · <b>entregadas — 196 tests en verde</b></span>')

rep('<tr><td class="mono">03</td><td>Capas Channel y System</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>E2E autenticado devuelve el breakdown completo a través de ambas capas</td></tr>',
    '<tr class="shipped"><td class="mono">03</td><td>Capas Channel y System</td><td><span class="done">ENTREGADA</span></td><td>E2E autenticado devuelve el breakdown completo atravesando ambas capas · 41 tests de API y seguridad</td></tr>')

rep('  <h3>Fases 00–02 — entregadas</h3>', '  <h3>Fases 00–03 — entregadas</h3>')

rep('''    <p><strong>155 tests en verde</strong> en seis proyectos, cero paquetes vulnerables. El motor reproduce exactamente los dos casos documentados: <span class="mono">EAI 14381 = 79,67</span> y <span class="mono">EAI 10969 = 79,61</span>, métrica por métrica.</p>
    <p>Y el 14381 ahora se reproduce <strong>extremo a extremo</strong>: generador → lectura por el puerto → ensamblado → scoring. Es la prueba de que cambiar de adaptador no puede cambiar un score.</p>''',
    '''    <p><strong>196 tests en verde</strong> en diez proyectos, cero paquetes vulnerables. El motor reproduce exactamente los dos casos documentados: <span class="mono">EAI 14381 = 79,67</span> y <span class="mono">EAI 10969 = 79,61</span>, métrica por métrica.</p>
    <p>El 14381 se reproduce <strong>extremo a extremo</strong>: generador → puerto <code>ISourceReader</code> → ensamblado → motor → <code>system-api</code> → <code>channel-api</code> → respuesta autenticada del BFF. Cada eslabón de esa cadena está cubierto por tests.</p>''')

# Entregables de fase 3
rep('        <tr><td class="mono">ADR 0001 – 0004</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, y huecos entre bandas del BRD</td></tr>',
    '''        <tr><td class="mono">Modernization.SystemApi</td><td>Minimal APIs con contratos explícitos, token de servicio en tiempo constante, caché de scores, OpenAPI y un único punto de decisión para el adaptador de datos</td></tr>
        <tr><td class="mono">Modernization.ChannelApi</td><td>BFF con OIDC (Authorization Code + PKCE), sesión en cookie <code>__Host-</code> httpOnly, CSRF de doble envío, rate limiting por usuario y cliente tipado con Polly</td></tr>
        <tr><td class="mono">ScreenComposer</td><td>Un modelo de vista por pantalla: portafolio con agregados, tarjetas de pilar del dashboard v1.3, y la pantalla de explicabilidad con fórmula, waterfall y procedencia</td></tr>
        <tr><td class="mono">Modernization.Security</td><td>Cabeceras de seguridad compartidas, validación de entrada por lista blanca y autenticación servicio a servicio</td></tr>
        <tr><td class="mono">Tests de API</td><td>E2E a través de ambas capas con tráfico HTTP real entre ellas, más 24 tests de seguridad: 401 en cada endpoint, atributos de la cookie, CSRF, CSP, y la guarda del esquema de desarrollo</td></tr>
        <tr><td class="mono">ADR 0001 – 0005</td><td>Arquitectura en dos capas, reglas como datos, ausencia de dato frente a cero, huecos entre bandas del BRD, y BFF con token de servicio</td></tr>''')

# Decisiones de fase 3
rep('    <li><strong>Sin generador de datos falsos genérico.</strong> El vocabulario está curado de las capturas — nombres de aplicación LATAM, CVEs y librerías reales, rangos RTO/RPO observados. Da datos más parecidos a los reales y evita una dependencia más en el árbol.</li>',
    '''    <li><strong>Sin generador de datos falsos genérico.</strong> El vocabulario está curado de las capturas — nombres de aplicación LATAM, CVEs y librerías reales, rangos RTO/RPO observados. Da datos más parecidos a los reales y evita una dependencia más en el árbol.</li>
    <li><strong>El gate de seguridad se disparó solo.</strong> <code>Microsoft.AspNetCore.OpenApi</code> arrastra <code>Microsoft.OpenApi</code> 2.0.0 con una vulnerabilidad alta conocida (GHSA-v5pm-xwqc-g5wc) y el build falló. Se resolvió con un pin transitivo a 2.12.2. El control del plan funcionando en su primer encuentro real.</li>
    <li><strong>El token CSRF va en hexadecimal, no en Base64.</strong> ASP.NET Core codifica en URL el valor de la cookie al escribirla, así que un valor con <code>+</code>, <code>/</code> o <code>=</code> nunca coincidiría con lo que el cliente lee de <code>document.cookie</code>. Lo detectó un test antes de que llegara al frontend.</li>
    <li><strong>Los errores no hacen eco de la entrada.</strong> Un EAI code se valida por lista blanca en las dos capas y se rechaza con 400 si no es una secuencia de hasta 10 dígitos. Devolver entrada del usuario dentro de un mensaje de error es como nacen los XSS reflejados.</li>
    <li><strong>Los agregados del portafolio cubren el conjunto filtrado, no la página.</strong> Calcularlos sobre la página daría un promedio distinto en cada scroll — un bug sutil que solo aparece al paginar.</li>''')

# Nueva subsección: superficie de API
anchor = '  <h4>Cobertura de las fuentes en el portafolio dummy</h4>'
api_section = '''  <h4>Superficie de API entregada</h4>
  <div class="prose"><p>La capa System expone recursos canónicos completos; la Channel expone un endpoint por pantalla que devuelve todo lo que esa pantalla necesita en una sola llamada.</p></div>
  <div class="tw">
    <table>
      <thead><tr><th style="width:12%">Capa</th><th style="width:34%">Endpoint</th><th>Devuelve</th></tr></thead>
      <tbody>
        <tr><td>System</td><td class="mono">GET /system/applications</td><td>Inventario paginado, filtrable por LOB, país, CIO, disposition y criticidad</td></tr>
        <tr><td>System</td><td class="mono">GET /system/applications/{eai}/score</td><td>Desglose completo: numerador, denominador, tres pilares, cada métrica con su regla y su procedencia</td></tr>
        <tr><td>System</td><td class="mono">GET /system/scores</td><td>Scores del portafolio, para agregados</td></tr>
        <tr><td>System</td><td class="mono">GET /system/sources</td><td>Estado, cobertura y rechazos por fuente</td></tr>
        <tr><td>System</td><td class="mono">GET /system/ruleset</td><td>Pesos, bandas y procedencia del ruleset activo</td></tr>
        <tr><td>Channel</td><td class="mono">GET /api/portfolio</td><td>Pantalla 01: filas, distribución por tramo, agregados por LOB, país y disposition</td></tr>
        <tr><td>Channel</td><td class="mono">GET /api/applications/{eai}/overview</td><td>Pantalla 02: score, tramo y tarjetas de pilar con peso nominal y disponible</td></tr>
        <tr><td>Channel</td><td class="mono">GET /api/applications/{eai}/explain</td><td>Pantalla 03: fórmula sustituida, waterfall acumulado, métricas incluidas, excluidas con motivo, e informativas</td></tr>
        <tr><td>Channel</td><td class="mono">GET /api/lineage</td><td>Pantalla 07: cobertura y rechazos por fuente</td></tr>
        <tr><td>Channel</td><td class="mono">GET /api/session · POST /api/logout</td><td>Identidad, token CSRF y cierre de sesión del lado servidor</td></tr>
      </tbody>
    </table>
  </div>

  <div class="formula">
    <div class="eq">GET /api/applications/14381/explain</div>
    <div class="def">
      <div><b>formula.expression</b>&nbsp; "20 + (44.75 / 60) x 80 = 79.67"</div>
      <div><b>waterfall[0]</b>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; Architectural Value · ponderado 9.75 · acumulado 9.75 · +13.0 pts del score final</div>
      <div><b>excluded[1]</b>&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; SCA Flaws (Critical) · peso 10 · "El scan SCA no reporto severidad Critical"</div>
      <div><b>included[].provenance</b> { system: "CMT", report: "...", field: "...", provider: "Dummy" }</div>
    </div>
  </div>

''' + anchor
rep(anchor, api_section)

io.open(p, 'w', encoding='utf-8').write(s)
print("ok")
