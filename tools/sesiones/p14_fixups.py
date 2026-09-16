"""Devuelve index.css a CRLF y hace determinista el ayudante de inicio de sesion."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")

css = root / "src/web/src/index.css"
text = css.read_text(encoding="utf-8", newline="")
css.write_text(text.replace("\r\n", "\n").replace("\n", "\r\n"), encoding="utf-8", newline="")
print("index.css devuelto a CRLF")

support = root / "src/web/e2e/support.ts"
text = support.read_text(encoding="utf-8", newline="")
crlf = "\r\n" in text
flat = text.replace("\r\n", "\n") if crlf else text

old = "  const portfolioTab = page.getByRole('link', { name: 'Portfolio', exact: true });"
new = (
    "  // El enlace del encabezado, no el de la navegacion de una aplicacion: la pantalla de\n"
    "  // calculo tiene los dos, y esperar \"cualquiera\" es una carrera con el render.\n"
    "  const portfolioTab = page.getByRole('banner').getByRole('link', { name: 'Portfolio', exact: true });"
)

if new in flat:
    raise SystemExit("ya aplicado")
if old not in flat:
    raise SystemExit("ancla no encontrada en support.ts")

flat = flat.replace(old, new, 1)
support.write_text(flat.replace("\n", "\r\n") if crlf else flat, encoding="utf-8", newline="")
print("support.ts: enlace del encabezado")
