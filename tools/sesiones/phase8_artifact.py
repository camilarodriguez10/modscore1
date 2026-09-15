import io
import os

HERE = os.path.dirname(os.path.abspath(__file__))
PATH = os.path.join(HERE, "mod-score-plan.html")

s = io.open(PATH, encoding="utf-8").read()


def rep(old, new):
    global s
    assert old in s, f"NO MATCH: {old[:90]!r}"
    s = s.replace(old, new, 1)


rep('<span>Fases 00–07 · <b>entregadas — 351 tests en verde</b></span>',
    '<span>Fases 00–08 · <b>entregadas — 435 tests en verde</b></span>')

rep('<tr><td class="mono">08</td><td>Endurecimiento</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>Sin hallazgos High o Critical abiertos</td></tr>',
    '<tr class="shipped"><td class="mono">08</td><td>Endurecimiento</td><td><span class="done">ENTREGADA</span></td><td><strong>Ningún hallazgo High ni Critical abierto</strong> · 23 hallazgos, las 3 Altas cerradas · ASVS L2 · modelo de amenazas · runbooks · prueba de carga</td></tr>')

rep('  <h3>Fases 00–07 — entregadas</h3>', '  <h3>Fases 00–08 — entregadas</h3>')

rep('<p><strong>351 tests en verde</strong> en doce proyectos, cero paquetes vulnerables.',
    '<p><strong>435 tests en verde</strong> en trece proyectos, cero paquetes vulnerables, y el build compila con todas las reglas de seguridad del analizador como errores.')

rep("<li><strong>CSP estricta con nonce por request</strong> — sin <code>unsafe-inline</code>, sin <code>unsafe-eval</code>, <code>object-src 'none'</code>, <code>frame-ancestors 'none'</code>.</li>",
    "<li><strong>CSP estricta, sin nonce</strong> — sin <code>unsafe-inline</code>, sin <code>unsafe-eval</code> y sin orígenes de terceros. El build no tiene un solo script ni estilo en línea, así que <code>'self'</code> es más estricto que un nonce. Verificado en navegador con cero violaciones; las fuentes se sirven desde el mismo origen.</li>")

rep("<li>Doble-submit CSRF token en todas las mutaciones, SRI y <code>Permissions-Policy</code> restrictiva.</li>",
    "<li>Doble envío de token CSRF en todas las mutaciones y <code>Permissions-Policy</code> restrictiva. SRI no aplica: todos los recursos son del mismo origen.</li>")

phase8 = '''
  <h3>Fase 08 — lo que encontró el endurecimiento</h3>
  <div class="ship">
    <div class="h">Criterio de aceptación cumplido</div>
    <p><strong>Ningún hallazgo High ni Critical abierto.</strong> La revisión encontró 23 hallazgos; las tres Altas aparecieron en esta fase y se cerraron con pruebas que las reproducen. 84 pruebas nuevas, y el build compila con <strong>todas</strong> las reglas de seguridad del analizador como errores.</p>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:18%">Severidad</th><th class="num" style="width:13%">Encontrados</th><th class="num" style="width:11%">Cerrados</th><th>Abiertos</th></tr></thead>
      <tbody>
        <tr><td><strong>Alta</strong></td><td class="num">3</td><td class="num">3</td><td>—</td></tr>
        <tr><td>Media</td><td class="num">10</td><td class="num">8</td><td>H-16 parcial: token de servicio estático · H-17: firma de imágenes y logs inmutables, infraestructura</td></tr>
        <tr><td>Baja</td><td class="num">8</td><td class="num">6</td><td>H-20: ver y cerrar sesiones activas · H-21: antivirus en cargas</td></tr>
        <tr><td>Informativa</td><td class="num">2</td><td class="num">2</td><td>—</td></tr>
      </tbody>
    </table>
  </div>

  <h4>Las tres Altas</h4>
  <div class="tw">
    <table>
      <thead><tr><th style="width:30%">Hallazgo</th><th>Qué pasaba</th><th style="width:32%">Corrección</th></tr></thead>
      <tbody>
        <tr><td><strong>Cualquier sesión podía publicar una carga</strong></td><td>Reemplazar el portafolio de toda la organización solo exigía estar autenticado</td><td>Rol <code>ingestion.write</code> y alcance completo, verificados en las dos capas</td></tr>
        <tr><td><strong>La autorización por LOB/CIO del plan no existía</strong></td><td>Todo usuario veía el portafolio completo, incluidas las listas de CVEs por aplicación: un mapa de qué atacar primero</td><td>Alcance aplicado en la capa System; ninguna lectura compila sin él; por defecto no se ve nada</td></tr>
        <tr><td><strong>El rate limiter corría antes de autenticar</strong></td><td>Nadie tenía balde propio: todos compartían el de la IP del ingress, y uno solo agotaba el servicio</td><td>Autenticar antes de limitar, partición por usuario y políticas propias para simulaciones y cargas</td></tr>
      </tbody>
    </table>
  </div>

  <h4>Medido antes y después</h4>
  <div class="tw">
    <table>
      <thead><tr><th>Con 30 versiones en la historia</th><th class="num" style="width:20%">Antes</th><th class="num" style="width:26%">Después</th></tr></thead>
      <tbody>
        <tr><td>Historia del portafolio</td><td class="num">1,0 s</td><td class="num">0,006 s</td></tr>
        <tr><td>Historia de una aplicación</td><td class="num">0,8 s</td><td class="num">0,005 s</td></tr>
        <tr><td>Pantalla Cargas, 16 usuarios concurrentes</td><td class="num">—</td><td class="num">4 241 req/s · p95 5,5 ms</td></tr>
        <tr><td>JSON de 2 MB contra cualquier endpoint</td><td class="num">aceptado</td><td class="num">413</td></tr>
        <tr><td>Carga de 40 MB</td><td class="num">cortada a 30 MB</td><td class="num">aceptada en 3,3 s</td></tr>
      </tbody>
    </table>
  </div>

  <h4>ASVS 4.0.3 · nivel 2</h4>
  <div class="prose"><p>Cada requisito aplicable con su estado y su evidencia: <strong>100 cumple</strong>; 15 parciales, verificables solo en un despliegue real —mTLS, TLS del ingress, gestor de secretos, SIEM—; 3 pendientes, de severidad baja o de infraestructura; y 12 a cargo del IdP o sin funcionalidad a la que aplicar.</p></div>

  <div class="note"><strong>Lo que no se ejecutó en local, y por qué.</strong> DAST con OWASP ZAP, Trivy y la construcción de las imágenes requieren descargar imágenes de contenedor. Están en el pipeline de CI.</div>
</section>

<hr class="div">

<!-- 8 -->'''
rep('\n</section>\n\n<hr class="div">\n\n<!-- 8 -->', phase8)

rep('        <tr><td class="mono">ADR 0001 – 0010</td><td>A los ocho anteriores se suman la historia como dimensión del portafolio y la carga de archivos como superficie de ataque</td></tr>',
    '''        <tr><td class="mono">docs/security</td><td>Modelo de amenazas STRIDE por frontera, matriz ASVS L2 con evidencia por requisito y registro de 23 hallazgos</td></tr>
        <tr><td class="mono">docs/runbooks</td><td>Rotación de secretos sin corte, carga defectuosa, recuperación ante desastres e incidente de seguridad</td></tr>
        <tr><td class="mono">.github/workflows/ci.yml</td><td>Tests, vulnerabilidades, CodeQL, Gitleaks sobre todo el historial, OSV, Trivy, Checkov y SBOM, con cada acción fijada por SHA</td></tr>
        <tr><td class="mono">deploy/</td><td>Imágenes chiseled no root · manifiestos con PSA restricted, filesystem de solo lectura, NetworkPolicy default-deny y mTLS</td></tr>
        <tr><td class="mono">tools/Modernization.LoadTest</td><td>Prueba de carga sin dependencias, con la que se midió cada corrección de rendimiento antes y después</td></tr>
        <tr><td class="mono">ADR 0001 – 0013</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos y los límites por usuario</td></tr>''')

marker = 'y el adaptador lo declara la capa System en lugar de inferirse de los datos.</li>'
rep(marker, marker + '''
    <li><strong>Las reglas de seguridad del analizador estaban apagadas sin avisar.</strong> <code>AnalysisModeSecurity=All</code> no tiene efecto cuando <code>AnalysisLevel</code> ya define modo, y el primer escaneo devolvió cero hallazgos. Con <code>AnalysisLevelSecurity=latest-all</code> aparecieron 64: uno real —el sufijo del identificador de carga salía de <code>Random</code>— y 62 deliberados en el generador de datos dummy.</li>
    <li><strong>El alcance es un parámetro que no se puede omitir.</strong> <code>PortfolioService</code> no tiene ninguna lectura sin <code>PortfolioScope</code>: un endpoint nuevo que olvide el alcance no compila, en lugar de devolver el portafolio completo.</li>
    <li><strong><code>SameSite=Strict</code> es parte del control de fijación de sesión.</strong> Con la sesión en el servidor, iniciar sesión sobre una cookie existente renueva la misma clave. En producción no ocurre porque el callback de OIDC es cross-site y la cookie no viaja; relajarla a <code>Lax</code> obliga a revisar la decisión.</li>
    <li><strong>Autenticado no significa inofensivo.</strong> Los límites asumen que el riesgo también está dentro: una cuenta comprometida no puede agotar el servicio, recalcular la historia sin fin ni publicar dos cargas a la vez.</li>''')

rep('├─ docs/           scoring-model.md · adr/ · rfcs/', '├─ docs/           adr/ · security/ · runbooks/')

rep('└─ deploy/                         <em>Helm, NetworkPolicies, mesh</em></pre>',
    '├─ .github/workflows/ci.yml        <em>gates de seguridad ✓</em>\n└─ deploy/{docker,k8s}             <em>chiseled · PSA restricted · NetworkPolicy · mTLS ✓</em></pre>')

rep('<div class="prose"><p>Siete puntos donde las capturas no alcanzan para cerrar el modelo. Ninguno bloquea el arranque — todos tienen un default implementado y son configurables.</p></div>',
    '<div class="prose"><p>Los puntos donde las capturas o la infraestructura no alcanzan para cerrar el modelo. Ninguno bloquea el arranque: todos tienen un default implementado y son configurables.</p></div>')

rfc07_start = s.index('<tr><td class="mono">RFC-07</td>')
rfc07_end = s.index('</tr>', rfc07_start) + len('</tr>')
s = s[:rfc07_end] + '''
        <tr><td class="mono">RFC-10</td><td>Claim de alcance del IdP</td><td>La autorización por LOB/CIO lee <code>portfolio_scope</code> con valores <code>*</code>, <code>lob:…</code>, <code>country:…</code>, <code>cio:…</code>. El nombre es configurable y, sin claim, no se ve nada. ¿Lo emite el IdP corporativo, o se deriva de grupos?</td></tr>
        <tr><td class="mono">RFC-11</td><td>Cache compartido de sesiones</td><td>Las sesiones viven en el servidor con cache en memoria: una sola réplica de channel-api hasta registrar Redis.</td></tr>
        <tr><td class="mono">RFC-12</td><td>Identidad de carga de trabajo</td><td>Token de servicio estático con rotación sin corte y mTLS. ¿Qué identidad de vida corta ofrece la plataforma del cluster?</td></tr>''' + s[rfc07_end:]

io.open(PATH, "w", encoding="utf-8").write(s)
print("ok artefacto; sections", s.count("<section"), "/", s.count("</section>"), "· tables", s.count("<table>"), "/", s.count("</table>"))
