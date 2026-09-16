"""Fase 14 - TD-05: punta a punta y accesibilidad en el CI."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")
path = root / ".github/workflows/ci.yml"
text = path.read_text(encoding="utf-8", newline="")
crlf = "\r\n" in text

anchor = """  # Un repositorio privado sin GitHub Code Security no tiene code scanning: subir el SARIF"""

job = """  # Punta a punta y accesibilidad (TD-04, TD-05). Levanta las dos capas y la SPA en el
  # runner y las recorre con Chromium: el Edge del equipo de desarrollo no existe aqui.
  e2e:
    name: SPA de punta a punta
    runs-on: ubuntu-24.04
    defaults:
      run:
        working-directory: src/web
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false

      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          dotnet-version: "10.0.x"
          cache: true
          cache-dependency-path: "**/packages.lock.json"

      - uses: actions/setup-node@820762786026740c76f36085b0efc47a31fe5020 # v7.0.0
        with:
          node-version: "24"
          cache: npm
          cache-dependency-path: src/web/package-lock.json

      - name: Instalar exactamente el lock, sin scripts de instalacion
        run: npm ci --ignore-scripts

      - name: Navegador de Playwright
        run: npx playwright install --with-deps chromium

      # Compilar antes de levantar: el primer dotnet run no tiene que caber en el
      # tiempo de espera del servidor de pruebas.
      - name: Compilar las dos capas
        run: dotnet build ../channel/Modernization.ChannelApi/Modernization.ChannelApi.csproj

      - name: Punta a punta y accesibilidad
        run: npm run test:e2e
        env:
          MODSCORE_E2E_CHANNEL: chromium

      - uses: actions/upload-artifact@043fb46d1a93c77aae656e7c1c64a875d1fc6a0a # v7.0.1
        if: failure()
        with:
          name: playwright-trazas
          path: src/web/test-results/
          if-no-files-found: ignore

"""

if "name: SPA de punta a punta" in text:
    raise SystemExit("ya aplicado")

old = anchor.replace("\n", "\r\n") if crlf else anchor
new = (job + anchor).replace("\n", "\r\n") if crlf else job + anchor

if old not in text:
    raise SystemExit("ancla no encontrada")

path.write_text(text.replace(old, new, 1), encoding="utf-8", newline="")
print("job agregado a ci.yml")
