from pathlib import Path

CRLF, LF = chr(13) + chr(10), chr(10)
ROOT = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(relative, pairs):
    p = ROOT / relative
    t = p.read_text(encoding="utf-8", newline="")
    fin = CRLF if CRLF in t else LF
    for old, new in pairs:
        o = old.replace(LF, fin)
        n = new.replace(LF, fin)
        count = t.count(o)
        assert count == 1, (relative, count, old[:90])
        t = t.replace(o, n)
    p.write_text(t, encoding="utf-8", newline="")
    print("ok", relative)
