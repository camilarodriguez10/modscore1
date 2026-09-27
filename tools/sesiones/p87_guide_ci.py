from patchlib import patch

patch(r"docs\guia-localhost.md", [
("""```bat
npm --prefix src\\web install
```""",
"""```bat
npm --prefix src\\web ci --ignore-scripts
```"""),
])
