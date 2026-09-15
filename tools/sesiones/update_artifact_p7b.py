import io
p = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad\mod-score-plan.html"
s = io.open(p, encoding='utf-8').read()

def rep(old, new, n=1):
    global s
    assert old in s, f"NO MATCH: {old[:100]!r}"
    s = s.replace(old, new, n)

rep('<tr><td>Datos</td><td>Puerto <code>ISourceReader</code> con tres adaptadores intercambiables: Dummy → Archivos → Base de datos</td></tr>',
    '''<tr><td>Datos</td><td>Puerto <code>ISourceReader</code> con tres adaptadores intercambiables: <span class="match">Dummy → Archivos</span> → Base de datos</td></tr>
        <tr><td>Historia</td><td>Cada carga publica una <strong>versión inmutable</strong>: el score se lee siempre junto al peso disponible de ese momento</td></tr>''')

# Pantalla 08 del plan original: la entregada es Cargas; Admin de rulesets queda pendiente.
rep('<div class="screen"><span class="i">08</span><div><div class="t">Admin · Rulesets</div><div class="d">Comparación de versiones de ruleset, simulación del impacto en todo el portafolio antes de publicar, e historial de overrides con su justificación.</div></div></div>',
    '''<div class="screen"><span class="i">08</span><div><div class="t">Cargas <span class="req">req. 8 · 9</span></div><div class="d">Subir los archivos del período y recalcular el portafolio, con el veredicto de cada archivo — filas leídas, filas rechazadas y el motivo — el historial de versiones publicadas y la evolución del score del portafolio a lo largo de las cargas.</div></div></div>
    <div class="screen"><span class="i">09</span><div><div class="t">Admin · Rulesets</div><div class="d">Comparación de versiones de ruleset, simulación del impacto en todo el portafolio antes de publicar, e historial de overrides con su justificación.</div></div></div>''')

io.open(p, 'w', encoding='utf-8').write(s)
print("ok")
