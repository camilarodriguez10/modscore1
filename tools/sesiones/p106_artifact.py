from patchlib import patch

patch(r"docs\plan\modscore-plan.html", [
("""      <span>Fases 00–13 entregadas · 14 y 15 en curso · dashboard ejecutivo · <b>770 pruebas .NET, 58 unitarias y 40 e2e en verde</b></span>""",
 """      <span>Fases 00–13 entregadas · 14 y 15 en curso · dashboard ejecutivo · pantalla Code · <b>828 pruebas .NET, 63 unitarias y 49 e2e en verde</b></span>"""),
# pantallas
("""y la pestaña de reglas donde el administrador edita las 37 definiciones de las seis preguntas, con borrador, simulación de los indicadores y publicación.</div></div></div>""",
 """y la pestaña de reglas donde el administrador edita las definiciones —37 de las seis preguntas y 14 de la pantalla Code—, con borrador, simulación de los indicadores y publicación.</div></div></div>
    <div class="screen key"><span class="i">13</span><div><div class="t">Code — obsolescencia y riesgo por componentes <span class="req">nueva</span></div><div class="d">Lo que declaran los repositorios de cada aplicación en GitHub Enterprise —runtimes, frameworks, servidores, imágenes de sistema operativo, bases de datos y bibliotecas— contra su fecha de fin de vida en endoflife.date y sus alertas abiertas: qué vence y cuándo, qué bibliotecas necesitan atención, dónde el código contradice al Currency Score de CMT y qué aplicaciones cargan más riesgo de código, con el detalle de cada una. Repositorios de demostración hasta la integración.</div></div></div>"""),
# plan de entrega
("""        <tr><td class="mono">16</td><td>Escala y operación</td>""",
 """        <tr class="shipped"><td class="mono">—</td><td>Pantalla Code · C1</td><td><span class="done">ENTREGADA</span></td><td><strong>Obsolescencia y riesgo según los componentes de cada repositorio</strong> · fin de vida de endoflife.date (22 productos, 273 ciclos) · 14 parámetros editables desde el portal · discrepancias con el Currency Score · repositorios de demostración que solo arrancan en Development (ADR 0026)</td></tr>
        <tr><td class="mono">—</td><td>Pantalla Code · C2 · GitHub Enterprise</td><td><div class="dur"><i style="width:48px"></i><span>3 sem</span></div></td><td>Recolector aparte con una GitHub App de solo lectura, mapeo repositorio → EAI y publicación del inventario a la capa System: la pantalla muestra una organización piloto real, sin credenciales de GitHub en la System ni en la Channel</td></tr>
        <tr><td class="mono">16</td><td>Escala y operación</td>"""),
("""    <p><strong>770 pruebas .NET en verde</strong>, más 58 unitarias y 40 de punta a punta en la SPA —diecinueve de flujo y veintiuna de accesibilidad—,""",
 """    <p><strong>828 pruebas .NET en verde</strong>, más 63 unitarias y 49 de punta a punta en la SPA —veinticuatro de flujo y veinticinco de accesibilidad—,"""),
# seccion nueva antes de la estructura del repositorio
("""  <h3>Estructura del repositorio</h3>""",
 """  <h3>Pantalla Code — obsolescencia y riesgo desde los repositorios</h3>
  <div class="ship">
    <div class="h">Pedida el 26 de septiembre · C1 entregada con datos de demostración</div>
    <p>El pedido: analizar el código de las aplicaciones directamente desde <strong>GitHub Enterprise</strong>, sin configurar todavía la integración, y construir con lo que se puede obtener de cada repositorio <strong>una vista de obsolescencia y riesgo según librerías, software y componentes</strong>, con una fuente para el fin de vida. La vista existe y responde con los mismos filtros y el mismo alcance que el resto; la integración queda planificada.</p>
    <p>La fuente del fin de vida es <strong>endoflife.date</strong>: una foto versionada de su API con 22 productos y 273 ciclos. El soporte extendido pagado se muestra pero no cuenta como soporte, porque depende de un contrato que el catálogo no conoce.</p>
  </div>

  <div class="tw">
    <table>
      <thead><tr><th style="width:30%">Pregunta de la pantalla</th><th>Qué muestra</th></tr></thead>
      <tbody>
        <tr><td><strong>¿Qué llegó o está por llegar a su fin de vida?</strong></td><td>Cada producto y ciclo en uso con su fecha, su estado —vencido, vence pronto, sin soporte activo, con soporte, no evaluable— y el enlace a la fuente; una barra por estado que filtra la tabla</td></tr>
        <tr><td><strong>¿Qué bibliotecas necesitan atención?</strong></td><td>Obsoletas, varias versiones mayores atrás o con alertas críticas y altas, con cuántas aplicaciones las usan</td></tr>
        <tr><td><strong>¿Dónde el código contradice al inventario?</strong></td><td><em>Hidden obsolescence</em>: vigente según CMT con componentes vencidos en el código. <em>Inventory behind the code</em>: legada según CMT sin nada vencido en el código</td></tr>
        <tr><td><strong>¿Quién carga más riesgo de código?</strong></td><td>Índice por aplicación —la suma de los puntos de cada hallazgo—, con su nivel y el detalle de dónde sale cada punto. Una aplicación sin repositorios no tiene nivel: no es riesgo bajo, es que no se ve</td></tr>
      </tbody>
    </table>
  </div>

  <h4>La integración: un recolector aparte que no se expone</h4>
  <pre>GitHub Enterprise ──(GitHub App, solo lectura)──► <b>code-collector</b>  <em>tarea programada, sin ingress</em>
endoflife.date · registros de paquetes ─────────►       │
                                                        │  inventario validado, por un endpoint interno
                                                        ▼
                                   <b>capa System</b>  <em>dueña del almacenamiento y del cálculo</em>
                                                        │
                                   <b>capa Channel</b> ──► SPA /code</pre>
  <ul class="prose">
    <li><strong>Solo lectura y solo metadatos.</strong> Permisos <em>Metadata</em>, <em>Contents</em> para una lista cerrada de manifiestos, <em>Dependabot alerts</em> y <em>Custom properties</em>. No clona, no guarda código y no ejecuta nada de los repositorios.</li>
    <li><strong>Ni la System ni la Channel tienen credenciales de GitHub.</strong> La clave de la GitHub App vive en el gestor de secretos y la usa solo el recolector, con tokens de una hora.</li>
    <li><strong>Repositorio → aplicación</strong> por una propiedad personalizada con el código EAI; lo que no tiene mapeo se informa aparte y no entra en ningún conteo.</li>
    <li><strong>El índice no toca el Modernization Score.</strong> El score lo define el BRD; la pantalla muestra la discrepancia con el Currency Score en lugar de corregirlo.</li>
  </ul>

  <div class="rfc">
    <b class="tag">Datos de demostración, dichos con todas sus letras</b>
    Hasta la integración, los repositorios se generan del portafolio vigente, deterministas por código EAI: el lenguaje del maestro elige la familia tecnológica y el Currency Score inclina las versiones. La pantalla lo avisa en ámbar antes que cualquier número, y ese proveedor <strong>no arranca fuera de Development</strong>, para que un dato inventado nunca se lea como real. Las decisiones para conectarla —la GitHub App, el mapeo, la salida de red, la fuente de vulnerabilidades, el refresco del catálogo y los puntos del índice— son <strong>GH-01 a GH-09</strong> en <code>docs/PENDING-ITEMS.md</code>.
  </div>

  <h3>Estructura del repositorio</h3>"""),
("""│  ├─ <b>dashboard-v1.json</b>           <em>valores de fábrica de las definiciones ✓</em>""",
 """│  ├─ <b>dashboard-v1.json</b>           <em>valores de fábrica de las definiciones ✓</em>
│  ├─ <b>eol-catalog-v1.json</b>         <em>fin de vida de endoflife.date, versionado ✓</em>"""),
("""  <div class="note">Desde la Fase 13 cada decisión abierta —del negocio sobre el score, de plataforma, de seguridad y de operación— tiene su identificador, contexto, opciones, recomendación y criterio de aceptación en <code>docs/PENDING-ITEMS.md</code>, en inglés para revisarla con el equipo. Se suman RFC-13 a RFC-17, la migración a OAuth con APIM y la visibilidad del repositorio.</div>""",
 """  <div class="note">Desde la Fase 13 cada decisión abierta —del negocio sobre el score, de plataforma, de seguridad y de operación— tiene su identificador, contexto, opciones, recomendación y criterio de aceptación en <code>docs/PENDING-ITEMS.md</code>, en inglés para revisarla con el equipo. Se suman RFC-13 a RFC-17, la migración a OAuth con APIM, la visibilidad del repositorio y, desde el 26 de septiembre, la integración con GitHub Enterprise (GH-01 a GH-09).</div>"""),
])
