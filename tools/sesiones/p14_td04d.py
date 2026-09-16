"""Arregla la insercion duplicada de los tokens de texto en modo oscuro."""
from pathlib import Path

css = Path(r"C:\Users\kmiro\Downloads\mod score\src\web\src\index.css")
text = css.read_text(encoding="utf-8", newline="")
crlf = "\r\n" in text
flat = text.replace("\r\n", "\n") if crlf else text

dup = """
  /* Sobre fondo oscuro los tres primeros ya pasan; el critico necesita aclararse. */
  --app-good-text: #0ca30c;
  --app-warning-text: #fab219;
  --app-serious-text: #ec835a;
  --app-critical-text: #d96060;
"""

if dup not in flat:
    raise SystemExit("no hay bloque duplicado que quitar")

flat = flat.replace(dup, "", 1)

# El bloque del tema fijo es el ultimo con ese wash.
anchor = "  --app-critical-wash: #2e1717;"
position = flat.rfind(anchor)
if position == -1:
    raise SystemExit("no se encontro el bloque del tema oscuro fijo")

end = position + len(anchor)
flat = flat[:end] + dup.rstrip("\n") + flat[end:]

css.write_text(flat.replace("\n", "\r\n") if crlf else flat, encoding="utf-8", newline="")
print("corregido")
