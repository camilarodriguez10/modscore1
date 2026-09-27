from patchlib import patch

patch("HANDOFF.md", [
("""El anterior se borra: cada paquete queda para siempre en la historia del repositorio |""",
 """El anterior se borra: cada paquete queda para siempre en la historia del repositorio. **Su `GUIA-PASO-A-PASO.txt` tiene el paso 3 del camino B anterior** (`npm install` y Node 22): se corrigió el 2026-09-21 en `docs/guia-localhost.md` sin regenerar el paquete, para no sumar otros 93 MB al historial por un cambio de texto que solo afecta a quien trabaja desde el código. Llega con el próximo build |"""),
])
