"""Artefacto del plan: cierre de la Fase 13 (revision y remediacion)."""
import io
import sys

PATH = sys.argv[1] if len(sys.argv) > 1 else r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"

raw = io.open(PATH, encoding="utf-8", newline="").read()
crlf = "\r\n" in raw
s = raw.replace("\r\n", "\n")

SECTION = """  <h3>Fase 13 — revisión y remediación</h3>
  <div class="ship">
    <div class="h">Los trece puntos de la revisión, entregados y probados</div>
    <p><strong>640 pruebas .NET, 35 unitarias y 6 de punta a punta en verde</strong>, cero paquetes vulnerables y <code>npm audit</code> limpio. Las reglas de cálculo se revisaron contra las 34 capturas del BRD y del dashboard, que mandan ante cualquier contradicción entre documentos.</p>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:30%">Pedido</th><th>Cómo quedó</th></tr></thead>
      <tbody>
        <tr><td><strong>Reglas del BRD primero</strong></td><td><em>"Null or zero values are excluded from scoring"</em>: el cero se excluye en Kenna, OSS, RAW Tech y Operational; Tech Debt y SCA lo siguen puntuando porque su tabla lo nombra (ADR 0019). Notas del ruleset corregidas contra las capturas y RFC-13 a RFC-17 nuevos. El ruleset tiene 18 métricas</td></tr>
        <tr><td><strong>Comparar reglas</strong></td><td>Vista nueva: dos versiones —o el borrador sin publicar— sobre todo el portafolio y sobre una aplicación, métrica por métrica</td></tr>
        <tr><td><strong>Cambios de reglas en la tendencia</strong></td><td>Una línea punteada donde se publicó cada versión, y un aviso de reglas nuevas para quien consulta</td></tr>
        <tr><td><strong>Descargas según los filtros</strong></td><td>CSV del portafolio, del roadmap agrupado y de la comparación, sin inyección de fórmulas, con límite propio y auditoría</td></tr>
        <tr><td><strong>Robustez</strong></td><td>Un almacén de reglas caído ya no deja sin scores; la reautenticación cierra la sesión anterior y queda lista para OAuth con APIM (ADR 0020)</td></tr>
        <tr><td><strong>Pruebas de la SPA</strong></td><td>Vitest para la lógica y Playwright con el Edge instalado; una prueba impide que los tipos TypeScript se separen del backend</td></tr>
        <tr><td><strong>Git y paquete portable</strong></td><td>Repositorio con un respaldo que se niega a subir las capturas del BRD; ejecutables de un solo archivo para probar en otro equipo, solo en la interfaz local</td></tr>
      </tbody>
    </table>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:44%">Medido antes y después</th><th>Antes</th><th>Después</th></tr></thead>
      <tbody>
        <tr><td>Historia del portafolio en frío, 30 cargas</td><td class="mono">1.655 ms</td><td class="mono">8 ms</td></tr>
        <tr><td>Simulación de reglas, p50 con 8 clientes</td><td class="mono">117 ms</td><td class="mono">57 ms</td></tr>
        <tr><td>Bundle principal de la SPA</td><td class="mono">429 kB</td><td class="mono">406 kB</td></tr>
      </tbody>
    </table>
  </div>

  <div class="note"><strong>Lo que sigue.</strong> Fase 14, verificación de seguridad con los 14 parciales de ASVS; Fase 15, autenticación con APIM; Fase 16, escala y operación; y al final, por decisión, la Fase 17 con H-16, H-17, H-20 y H-21. Los pendientes para el equipo, en inglés y con el máximo detalle, están en <code>docs/PENDING-ITEMS.md</code>. Nada se subió todavía al repositorio remoto: antes hay que confirmar su visibilidad.</div>

"""

ROWS = """
        <tr class="shipped"><td class="mono">13</td><td>Revisión y remediación</td><td><span class="done">ENTREGADA</span></td><td><strong>Reglas revisadas contra las capturas del BRD</strong> · comparación de reglas · descargas según filtros · pruebas de la SPA · git y paquete portable</td></tr>
        <tr><td class="mono">14</td><td>Verificación de seguridad</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>Primera corrida del pipeline con todos los gates y los 14 parciales de ASVS L2 verificados en un entorno de prueba</td></tr>
        <tr><td class="mono">15</td><td>Autenticación con APIM</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>OAuth mediante Azure API Management, claim de alcance y sin secreto estático entre capas</td></tr>
        <tr><td class="mono">16</td><td>Escala y operación</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>Destino y motor de base, Redis, dos réplicas por capa, respaldos, SIEM y notificaciones de reglas</td></tr>
        <tr><td class="mono">17</td><td>Hallazgos de infraestructura</td><td><div class="dur"><i style="width:32px"></i><span>2 sem</span></div></td><td>Al final, por decisión: identidad de carga de trabajo (H-16), firma de imágenes y logs inmutables (H-17), sesiones activas (H-20) y antivirus en cargas (H-21)</td></tr>"""

ROW12 = '<tr class="shipped"><td class="mono">12</td><td>Administración de reglas</td><td><span class="done">ENTREGADA</span></td><td><strong>Un peso cambiado desde el portal mueve el portafolio, queda auditado y lo toman las réplicas sin desplegar</strong> · borrador, simulación sobre todo el portafolio y versiones inmutables · reautenticación</td></tr>'

SEC09 = '  <div class="sec-head"><span class="sec-num">09</span><h2>Decisiones pendientes de confirmación</h2></div>'

RFC12 = '<tr><td class="mono">RFC-12</td><td>Identidad de carga de trabajo</td><td>Token de servicio estático con rotación sin corte y mTLS. ¿Qué identidad de vida corta ofrece la plataforma del cluster?</td></tr>'

RFC_NEW = """
        <tr><td class="mono">RFC-13</td><td><em>"Null or zero values are excluded"</em></td><td>El cero se excluye como un nulo en Kenna, OSS, RAW Tech y Operational; donde la tabla del BRD lo puntúa —Tech Debt 0, SCA sin flaws— sigue puntuando (ADR 0019). ¿Un Kenna 0 es "sin riesgo" o "sin escaneo"?</td></tr>
        <tr><td class="mono">RFC-14</td><td>DevSecOps <span class="mono">0/NULL–3 → 20</span></td><td>El BRD puntúa el nulo. Sin efecto mientras la métrica tenga peso 0.</td></tr>
        <tr><td class="mono">RFC-15</td><td><code>Consolidate</code> en la disposición</td><td>No figura en la tabla de puntaje del BRD; viene del modelo MAPS y se mapea a 20 por supuesto.</td></tr>
        <tr><td class="mono">RFC-16</td><td>Containerized <em>"added to product strategy (2 pts) and architecture value (2 pts)"</em></td><td>El dashboard y el ejemplo 14381 usan compuestos de peso 10. ¿Texto obsoleto o bonificación dentro de los compuestos?</td></tr>
        <tr><td class="mono">RFC-17</td><td>Filas con <span class="mono">EAI Code = 0</span> en OSS</td><td>No son aplicaciones y hoy no cruzan con nada, en silencio. ¿Se reportan como filas huérfanas en el linaje?</td></tr>"""

pairs = [
    (
        "los valores nulos o cero se excluyen del scoring;",
        "los valores nulos o cero se excluyen del scoring —el cero, salvo donde la tabla de la métrica lo puntúa, como Tech Debt 0 o SCA sin flaws (ADR 0019)—;",
    ),
    (
        '<tr><td>Kenna Score</td><td>670–1000 (Critical/High) <span class="chip c20">20</span> · 340–669 (Medium) <span class="chip c60">60</span> · 0–339 (Low) <span class="chip c100">100</span></td></tr>',
        '<tr><td>Kenna Score</td><td>670–1000 (Critical/High) <span class="chip c20">20</span> · 340–669 (Medium) <span class="chip c60">60</span> · 1–339 (Low) <span class="chip c100">100</span> · 0 <span class="chip cnull">sin dato</span></td></tr>',
    ),
    (
        '<tr><td>Open-Source Score</td><td>75–100 <span class="chip c100">100</span> · 60–74 <span class="chip c60">60</span> · 0–59 <span class="chip c20">20</span></td></tr>',
        '<tr><td>Open-Source Score</td><td>75–100 <span class="chip c100">100</span> · 60–74 <span class="chip c60">60</span> · 0–59 <span class="chip c20">20</span> · 0 exacto <span class="chip cnull">sin dato</span></td></tr>',
    ),
    (
        '<tr><td>RAW Tech Score</td><td>75–100 <span class="chip c100">100</span> · 60–74 <span class="chip c60">60</span> · 0–59 <span class="chip c20">20</span></td></tr>',
        '<tr><td>RAW Tech Score</td><td>75–100 <span class="chip c100">100</span> · 60–74 <span class="chip c60">60</span> · 0–59 <span class="chip c20">20</span> · 0 exacto <span class="chip cnull">sin dato</span></td></tr>',
    ),
    (
        '<tr><td>Operational Score</td><td>80–100 <span class="chip c100">100</span> · 60–79,99 <span class="chip c60">60</span> · 0–59,9 <span class="chip c20">20</span></td></tr>',
        '<tr><td>Operational Score</td><td>80–100 <span class="chip c100">100</span> · 60–79,99 <span class="chip c60">60</span> · 0–59,9 <span class="chip c20">20</span> · 0 exacto <span class="chip cnull">sin dato</span></td></tr>',
    ),
    (
        "Default: <strong>10</strong>, porque reproduce los números reales.",
        "Default: <strong>10</strong>, el peso publicado en el dashboard. Ningún caso dorado tiene Tech Debt, así que la elección <strong>no está verificada numéricamente</strong>.",
    ),
    (RFC12, RFC12 + RFC_NEW),
    (
        "<p>Vite 6 y TypeScript en modo estricto, React Router 7 con data APIs, TanStack Query v5 para el estado del servidor, Zod compartido con el backend para validación, Recharts para el waterfall y el gauge, y Tailwind 4 sobre primitivas Radix. El cliente de API se genera desde el OpenAPI del gateway, así los tipos no se desincronizan.</p>",
        "<p>Vite 7 y TypeScript 5.9 en modo estricto, React Router 7 con los filtros en la URL, TanStack Query v5 para el estado del servidor, gráficos en SVG propio y Tailwind 4 con componentes propios. Sin librerías de formularios ni de estado: el servidor valida y es la fuente de verdad. Los tipos del API se escriben a mano y una prueba de contrato impide que se separen de lo que serializa la capa Channel.</p>",
    ),
    (
        "<p>De React 19 se aprovechan tres cosas concretas: <strong>Actions y <code>useActionState</code></strong> para los formularios de what-if con estado pendiente sin boilerplate, <strong><code>use()</code> con Suspense</strong> para el streaming del breakdown, y el <strong>compilador</strong> para evitar memoización manual en las tablas grandes del portafolio.</p>",
        "<p>Se había prometido usar Actions con <code>useActionState</code>, <code>use()</code> con Suspense y el compilador de React 19. <strong>No se usan</strong>: las mutaciones de TanStack Query cubren los estados pendientes y no hubo tablas con costo de render medible. Sí se usa <code>lazy</code> con Suspense para cargar las pantallas de administración bajo demanda. Las pruebas son Vitest para la lógica pura y Playwright con el Edge instalado para los flujos completos.</p>",
    ),
    (
        "<span>Fases 00–12 · <b>entregadas — 598 tests en verde</b></span>",
        "<span>Fases 00–13 · <b>entregadas — 640 pruebas .NET, 35 unitarias y 6 e2e en verde</b></span>",
    ),
    (ROW12, ROW12 + ROWS),
    ("<h3>Fases 00–12 — entregadas</h3>", "<h3>Fases 00–13 — entregadas</h3>"),
    (
        "<p><strong>598 tests en verde</strong> en quince proyectos,",
        "<p><strong>640 pruebas .NET en verde</strong> en quince proyectos, más 35 unitarias y 6 de punta a punta en la SPA,",
    ),
    (
        "17 métricas con pesos, bandas, casos, compuestos, procedencia y nota de trazabilidad al BRD o al dashboard",
        "18 métricas con pesos, bandas, casos, compuestos, la regla del cero, procedencia y nota de trazabilidad al BRD o al dashboard",
    ),
    (
        '<tr><td class="mono">ADR 0001 – 0018</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos, los límites por usuario, la base que guarda la forma de las fuentes, el archivo que se reconoce por su encabezado, el filtro que vive en la URL y solo acota, el backend que no redacta frases y las reglas que se publican desde el portal como versiones</td></tr>',
        '<tr><td class="mono">ADR 0001 – 0020</td><td>Se suman la sesión en el servidor, el alcance aplicado sobre los datos, los límites por usuario, la base que guarda la forma de las fuentes, el archivo que se reconoce por su encabezado, el filtro que vive en la URL y solo acota, el backend que no redacta frases, las reglas que se publican desde el portal como versiones, el cero que se excluye salvo que la tabla del BRD lo puntúe y el camino de OIDC a OAuth con APIM</td></tr>',
    ),
    ("  <h3>Estructura del repositorio</h3>", SECTION + "  <h3>Estructura del repositorio</h3>"),
    (
        SEC09,
        SEC09 + '\n  <div class="note">Desde la Fase 13 cada decisión abierta —del negocio sobre el score, de plataforma, de seguridad y de operación— tiene su identificador, contexto, opciones, recomendación y criterio de aceptación en <code>docs/PENDING-ITEMS.md</code>, en inglés para revisarla con el equipo. Se suman RFC-13 a RFC-17, la migración a OAuth con APIM y la visibilidad del repositorio.</div>',
    ),
]

for old, new in pairs:
    count = s.count(old)
    assert count == 1, (count, old[:90])
    s = s.replace(old, new)

if crlf:
    s = s.replace("\n", "\r\n")

io.open(PATH, "w", encoding="utf-8", newline="").write(s)
print("ok artefacto")
