"""Fase 14 - segunda pasada: estado de la fase en README y en el plan de fases."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if old not in text:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:160])
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


patch("README.md", [
    (
        "| 14 Verificación de seguridad (ASVS L2) | planificada |",
        "| 14 Verificación de seguridad (ASVS L2) | en curso: hecho lo que no necesita un entorno real (PLAN §8.7) |",
    ),
])

patch("docs/PENDING-ITEMS.md", [
    (
        "| 14 | Security verification (ASVS L2) | First CI run and fixes; ASVS partial items verified in a test environment; peer security review; e2e and axe in CI | SEC-01…14, SEC-19, SEC-20, TD-04, TD-05, TD-09, TD-13 |",
        "| 14 | Security verification (ASVS L2) | **In progress.** Done on 2026-09-15: CI green, SEC-06, SEC-07, SEC-12, SEC-14, and e2e with axe in CI (TD-04, TD-05). Left: the eleven partials that need a test environment, ZAP, and the peer review | SEC-01…05, SEC-08…11, SEC-13, SEC-19, SEC-20, TD-09, TD-13 |",
    ),
    (
        "| Technical debt | TD-01 … TD-15 | None blocks; TD-06 and TD-09 are P1 |",
        "| Technical debt | TD-01 … TD-15 | None blocks; TD-06 and TD-09 are P1. TD-05 done and TD-04 automated on 2026-09-15 |",
    ),
])
