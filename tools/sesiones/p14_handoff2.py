"""Fase 14 - HANDOFF: encabezado y lecciones aprendidas."""
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


patch("HANDOFF.md", [
    (
        "**Fecha:** 2026-09-15 · **Estado:** fases 00 a 13 entregadas · **Siguiente:** Fase 14 (verificación de seguridad) y decisiones de negocio en paralelo",
        "**Fecha:** 2026-09-15 · **Estado:** fases 00 a 13 entregadas; Fase 14 en curso · **Siguiente:** terminar la Fase 14 en un entorno de prueba y decisiones de negocio en paralelo",
    ),
    (
        """- Una prueba de contrato entre C# y TypeScript da la garantía de un cliente generado sin
  sumar herramientas a la cadena de suministro.""",
        """- Una prueba de contrato entre C# y TypeScript da la garantía de un cliente generado sin
  sumar herramientas a la cadena de suministro.
- En Playwright, esperar «el enlace Portfolio» falla en la pantalla de cálculo, que tiene
  dos: el del encabezado y el de su navegación. La carrera aparece y desaparece según lo
  rápido que renderice. El ayudante apunta al del encabezado (`getByRole('banner')`).
- axe encuentra lo que la revisión a ojo no: un color atenuado a 4,42:1 y colores de estado
  usados como letra a 1,65:1. Los colores de marca y los de texto son dos cosas distintas y
  conviene que sean dos tokens distintos.""",
    ),
    (
        """- Fijar una acción por SHA no fija la herramienta que instala: `trivy-action` v0.36.0 baja
  Trivy v0.70.0, que tiene avisos de seguridad posteriores. Hay que fijar también su
  `version`.""",
        """- Fijar una acción por SHA no fija la herramienta que instala: `trivy-action` v0.36.0 baja
  Trivy v0.70.0, que tiene avisos de seguridad posteriores. Hay que fijar también su
  `version`.
- `sed -i` sobre un archivo con finales de línea CRLF lo deja en LF y el diff se vuelve el
  archivo entero. Revisar `git diff --numstat` después de editar con `sed`; los scripts de
  parche en Python conservan los finales de línea a propósito.""",
    ),
])
