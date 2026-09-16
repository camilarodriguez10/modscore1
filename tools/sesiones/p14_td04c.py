"""Fase 14 - TD-04: variantes de texto de los colores de estado (contraste AA)."""
import re
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")
web = root / "src/web/src"

NAMES = ("good", "warning", "serious", "critical")

LIGHT = """
  /* Los colores de estado de arriba son marcas: vivos, con etiqueta al lado. Como letra
     no llegan a 4.5:1, asi que el texto usa estas variantes (WCAG AA sobre surface y
     sobre su propio wash). */
  --app-good-text: #097d09;
  --app-warning-text: #936503;
  --app-serious-text: #bd4516;
  --app-critical-text: #c32f2f;
"""

DARK = """
    /* Sobre fondo oscuro los tres primeros ya pasan; el critico necesita aclararse. */
    --app-good-text: #0ca30c;
    --app-warning-text: #fab219;
    --app-serious-text: #ec835a;
    --app-critical-text: #d96060;
"""

DARK_THEME = DARK.replace("\n    ", "\n  ")


def edit(path, fn):
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    flat = text.replace("\r\n", "\n") if crlf else text
    changed = fn(flat)
    if changed == flat:
        return False
    path.write_text(changed.replace("\n", "\r\n") if crlf else changed, encoding="utf-8", newline="")
    return True


# ------------------------------------------------------------------ tokens
css = root / "src/web/src/index.css"


def add_tokens(text):
    if "--app-good-text" in text:
        raise SystemExit("los tokens de texto ya existen")

    text = text.replace(
        "  --app-critical-wash: #f9e5e5;\n}",
        "  --app-critical-wash: #f9e5e5;\n" + LIGHT + "}",
        1,
    )
    text = text.replace(
        "    --app-critical-wash: #2e1717;",
        "    --app-critical-wash: #2e1717;\n" + DARK.rstrip("\n"),
        1,
    )
    text = text.replace(
        "  --app-critical-wash: #2e1717;",
        "  --app-critical-wash: #2e1717;\n" + DARK_THEME.rstrip("\n"),
        1,
    )
    # El gris atenuado tambien se usa sobre el wash del acento en modo oscuro.
    text = text.replace("--app-ink-3: #828f96;", "--app-ink-3: #8b989e;")
    return text


print("index.css:", edit(css, add_tokens))

# ------------------------------------------------- uso como texto en la SPA
pattern = re.compile(r"var\(--app-(" + "|".join(NAMES) + r")\)")
changes = []


def as_text(text, path):
    lines = text.split("\n")
    for i, line in enumerate(lines):
        is_text_color = ("color:" in line) or re.search(r"\btext: '", line)
        if not is_text_color:
            continue
        new = pattern.sub(lambda m: "var(--app-" + m.group(1) + "-text)", line)
        if new != line:
            lines[i] = new
            changes.append(f"{path.relative_to(root)}:{i + 1}")
    return "\n".join(lines)


for tsx in sorted(web.rglob("*.tsx")):
    edit(tsx, lambda text, p=tsx: as_text(text, p))

# Casos donde el color no esta en la misma linea que "color:".
def multiline(text):
    return text.replace(
        "shown.deltaFromPrevious >= 0 ? 'var(--app-good)' : 'var(--app-critical)',",
        "shown.deltaFromPrevious >= 0 ? 'var(--app-good-text)' : 'var(--app-critical-text)',",
    )


if edit(web / "components/ScoreTrend.tsx", multiline):
    changes.append("src/web/src/components/ScoreTrend.tsx (delta)")


def ingestion(text):
    return text.replace(
        "  const color = result.accepted ? 'var(--app-good)' : 'var(--app-critical)';",
        "  const color = result.accepted ? 'var(--app-good-text)' : 'var(--app-critical-text)';",
    )


if edit(web / "routes/IngestionPage.tsx", ingestion):
    changes.append("src/web/src/routes/IngestionPage.tsx (veredicto)")


def roadmap(text):
    for name in ("critical", "serious", "warning"):
        text = text.replace(
            "return { color: 'var(--app-" + name + ")', wash:",
            "return { color: 'var(--app-" + name + "-text)', wash:",
        )
    return text


if edit(web / "routes/RoadmapPage.tsx", roadmap):
    changes.append("src/web/src/routes/RoadmapPage.tsx (avisos)")

print("lineas cambiadas:", len(changes))
for change in changes:
    print("  ", change)
