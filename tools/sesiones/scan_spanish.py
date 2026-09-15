"""Inventario de texto para personas en espanol dentro de literales de codigo (no comentarios)."""
import collections
import io
import json
import os
import re
import sys

ROOT = r"C:\Users\kmiro\Downloads\mod score"

SPANISH = re.compile(
    r"[áéíóúñ¿¡]|\b(de|la|el|los|las|que|con|para|por|una|un|sin|del|se|es|no hay|esta|este|"
    r"archivo|archivos|fuente|fuentes|carga|cargas|aplicacion|aplicaciones|columna|columnas|fila|filas|"
    r"metrica|metricas|peso|puntaje|tramo|portafolio|semanas|invalido|invalida|esperaba|recibio|"
    r"alcance|debe|entre|hasta|mas|menos|falta|faltan|desconocido|desconocida|encontrada|encontrado|"
    r"escenario|palanca|palancas|objetivo|techo|critico|solido|riesgo|aceptable|asignar|filtrado)\b",
    re.IGNORECASE,
)

def strip_cs_comments(text):
    out, i, n = [], 0, len(text)
    in_str = None
    while i < n:
        c = text[i]
        if in_str is None:
            if text.startswith("//", i):
                j = text.find("\n", i)
                i = n if j < 0 else j
                continue
            if text.startswith("/*", i):
                j = text.find("*/", i + 2)
                i = n if j < 0 else j + 2
                continue
            if c == '"':
                verbatim = i > 0 and text[i - 1] == "@"
                in_str = "v" if verbatim else "n"
            out.append(c)
        else:
            out.append(c)
            if in_str == "n" and c == "\\":
                out.append(text[i + 1] if i + 1 < n else "")
                i += 2
                continue
            if c == '"':
                if in_str == "v" and i + 1 < n and text[i + 1] == '"':
                    out.append('"')
                    i += 2
                    continue
                in_str = None
        i += 1
    return "".join(out)

LITERAL = re.compile(r'\$?@?"((?:[^"\\\n]|\\.)*)"')

hits = collections.defaultdict(list)
for base, _, files in os.walk(os.path.join(ROOT, "src")):
    if any(part in base for part in ("\\bin", "\\obj", "node_modules", "\\web", "Migrations")):
        continue
    for name in files:
        if not name.endswith(".cs"):
            continue
        path = os.path.join(base, name)
        text = strip_cs_comments(io.open(path, encoding="utf-8").read())
        for lineno, line in enumerate(text.splitlines(), 1):
            if "LoggerMessage" in line or "Message =" in line and "Logger" in line:
                continue
            for m in LITERAL.finditer(line):
                value = m.group(1)
                if len(value) > 3 and SPANISH.search(value):
                    hits[os.path.relpath(path, ROOT)].append((lineno, value[:110]))

total = 0
for path in sorted(hits):
    print(f"\n## {path} ({len(hits[path])})")
    for lineno, value in hits[path]:
        print(f"  {lineno}: {value}")
    total += len(hits[path])
print(f"\nTOTAL literales C#: {total} en {len(hits)} archivos")

# Contratos JSON: campos de texto en espanol
print("\n# Contratos JSON")
for base, _, files in os.walk(os.path.join(ROOT, "contracts")):
    for name in files:
        if not name.endswith(".json"):
            continue
        path = os.path.join(base, name)
        data = json.load(io.open(path, encoding="utf-8"))
        fields = collections.Counter()
        def walk(node, key=""):
            if isinstance(node, dict):
                for k, v in node.items():
                    walk(v, k)
            elif isinstance(node, list):
                for v in node:
                    walk(v, key)
            elif isinstance(node, str) and SPANISH.search(node) and len(node) > 3:
                fields[key] += 1
        walk(data)
        print(f"  {os.path.relpath(path, ROOT)}: {dict(fields)}")
