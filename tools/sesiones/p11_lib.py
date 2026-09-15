"""Utilidades de parcheo: reemplazos exactos que fallan si el ancla no existe."""
import io
import os

ROOT = r"C:\Users\kmiro\Downloads\mod score"


def patch(name, pairs, replace_all=False):
    path = os.path.join(ROOT, name)
    raw = io.open(path, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        if old not in s:
            if new in s:
                continue
            raise AssertionError(f"NO MATCH in {name}: {old[:120]!r}")
        s = s.replace(old, new) if replace_all else s.replace(old, new, 1)
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print("ok", name)


def translate(name, pairs):
    """Reemplaza valores JSON completos ("texto") en todas sus apariciones."""
    path = os.path.join(ROOT, name)
    s = io.open(path, encoding="utf-8", newline="").read()
    for old, new in pairs:
        needle = '"' + old + '"'
        if needle not in s:
            if ('"' + new + '"') in s:
                continue
            raise AssertionError(f"NO MATCH in {name}: {old[:80]!r}")
        s = s.replace(needle, '"' + new + '"')
    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print("ok", name)


def ensure_using(name, using, after_namespace_line=None):
    path = os.path.join(ROOT, name)
    raw = io.open(path, encoding="utf-8", newline="").read()
    line = f"using {using};"
    if line in raw:
        return
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    lines = s.split("\n")
    last_using = max((i for i, l in enumerate(lines) if l.startswith("using ")), default=-1)
    lines.insert(last_using + 1, line)
    s = "\n".join(lines)
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print("using", using, "->", name)
