import io, os
root = r"C:\Users\kmiro\Downloads\mod score"
scratch = r"C:\Users\kmiro\AppData\Local\Temp\claude\C--Users-kmiro-Downloads-mod-score\23c23612-74fe-439d-bd67-cdcfaeb9c2ca\scratchpad"

def load(p):
    s = io.open(p, encoding='utf-8', newline='').read()
    return s.replace('\r\n', '\n'), '\r\n' in s

def save(p, s, crlf):
    if crlf:
        s = s.replace('\n', '\r\n')
    io.open(p, 'w', encoding='utf-8', newline='').write(s)

# ---------------------------------------------------------------- PLAN.md
p = os.path.join(root, "PLAN.md")
s, crlf = load(p)
old = "| Retención | Manual por ahora; el directorio es la unidad de borrado |"
assert old in s, "PLAN: fila de retencion"
s = s.replace(old, old + "\n| Primer arranque | Sin ninguna carga las pantallas responden **409 con la salida** —ir a Cargas— y la carga sigue habilitada. El adaptador activo lo declara la capa System (`/system/ingestion/capabilities`); deducirlo de la primera versión bloqueaba la carga justo cuando no hay ninguna |", 1)
s = s.replace("343 tests", "351 tests")
save(p, s, crlf)
print("ok PLAN.md")

# ---------------------------------------------------------------- README.md
p = os.path.join(root, "README.md")
s, crlf = load(p)
assert "343 tests" in s, "README: conteo"
s = s.replace("343 tests", "351 tests")
old = """Para arrancar necesitas al menos una carga."""
assert old in s, "README: arranque"
s = s.replace(old, """En el primer arranque no hay ninguna carga: las pantallas lo dicen y llevan a
**Cargas**, que es la única que funciona sin datos. Para arrancar necesitas al menos una carga.""", 1)
save(p, s, crlf)
print("ok README.md")

# ---------------------------------------------------------------- artefacto
p = os.path.join(scratch, "mod-score-plan.html")
s, crlf = load(p)
assert s.count("343 tests en verde") == 2, s.count("343 tests en verde")
s = s.replace("343 tests en verde", "351 tests en verde")

old = '<tr><td>Retención</td><td>Manual: el directorio es la unidad de borrado</td><td>Política declarativa: N versiones o M meses</td></tr>'
assert old in s, "artefacto: fila retencion"
s = s.replace(old, old + '\n        <tr><td>Primer arranque</td><td>Las pantallas responden 409 con la salida —ir a Cargas— y la carga sigue habilitada</td><td>El mismo estado: una tabla de snapshots vacía</td></tr>', 1)

old = 'El orden de las guardas es parte del control.</li>'
assert old in s, "artefacto: bullet guardas"
s = s.replace(old, old + '''
    <li><strong>El primer arranque tiene que tener salida.</strong> Con el adaptador de archivos y ninguna carga, todas las pantallas devolvían 500 — y la de Cargas deshabilitaba la subida, porque deducía el adaptador activo leyendo la primera versión, que no existía. El sistema quedaba bloqueado sin salida desde la interfaz. Ahora la falta de datos es un 409 con un mensaje que lleva a Cargas, y el adaptador lo declara la capa System en lugar de inferirse de los datos.</li>''', 1)

save(p, s, crlf)
print("ok artefacto")
