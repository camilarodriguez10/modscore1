"""Regenera contracts/eol-catalog-v1.json desde la API de endoflife.date (ADR 0026).

Uso, desde la raíz del repositorio:

    python tools/eol-catalog/build-eol-catalog.py
    python tools/eol-catalog/build-eol-catalog.py --from <carpeta>

Descarga un JSON por producto, arma la foto con la fecha de hoy y la escribe. No ejecuta
nada de lo que descarga: solo lo lee como JSON. La verificación TLS no se desactiva nunca:
si Python no puede validar el certificado en ese equipo, se bajan los archivos con otra
herramienta (`curl -o nodejs.json https://endoflife.date/api/nodejs.json`, uno por producto
de la lista) y se pasan con `--from`. Después hay que correr
`dotnet test tests/Modernization.Scoring.Engine.Tests --filter EndOfLifeCatalog`, que valida
la foto y fija algunas fechas conocidas, y revisar la distribución de estados de la pantalla
Code: las versiones "actuales" del generador de demostración pueden haber vencido.

Solo biblioteca estándar de Python 3.
"""
import datetime
import json
import sys
import urllib.request
from pathlib import Path

API = "https://endoflife.date/api/{}.json"
OUT = Path(__file__).resolve().parents[2] / "contracts" / "eol-catalog-v1.json"

PRODUCTS = [
    # identificador de endoflife.date, nombre, clase
    ("nodejs", "Node.js", "runtime"),
    ("dotnet", ".NET", "runtime"),
    ("dotnetfx", ".NET Framework", "runtime"),
    ("oracle-jdk", "Oracle Java SE", "runtime"),
    ("eclipse-temurin", "Eclipse Temurin", "runtime"),
    ("python", "Python", "runtime"),
    ("go", "Go", "runtime"),
    ("angular", "Angular", "framework"),
    ("angularjs", "AngularJS", "framework"),
    ("react", "React", "framework"),
    ("vue", "Vue", "framework"),
    ("jquery", "jQuery", "framework"),
    ("spring-framework", "Spring Framework", "framework"),
    ("spring-boot", "Spring Boot", "framework"),
    ("django", "Django", "framework"),
    ("tomcat", "Apache Tomcat", "server"),
    ("log4j", "Apache Log4j", "library"),
    ("ubuntu", "Ubuntu", "os"),
    ("windows-server", "Windows Server", "os"),
    ("mssqlserver", "SQL Server", "database"),
    ("oracle-database", "Oracle Database", "database"),
    ("postgresql", "PostgreSQL", "database"),
]

# Ciclos con nombre comercial distinto del identificador.
LABELS = {
    "mssqlserver": {
        "17.0": "2025", "16.0": "2022", "15.0": "2019", "14.0": "2017",
        "13.0-sp3": "2016 SP3", "12.0-sp3": "2014 SP3", "11.0-sp4": "2012 SP4",
    },
    "windows-server": {"2012-r2": "2012 R2"},
    "oracle-database": {
        "23": "23ai", "21": "21c", "19": "19c", "18": "18c",
        "12.2": "12c R2", "12.1": "12c R1", "11.2": "11g R2",
    },
}

# Solo los ciclos con soporte general: ni los canales semestrales de Windows ni cada
# service pack de SQL Server.
KEEP = {
    "mssqlserver": set(LABELS["mssqlserver"]),
    "windows-server": {"2025", "2022", "2019", "2016", "2012-r2", "2012"},
}

# Productos cuyo historial completo interesa aunque sea anterior a 2012.
ALL_CYCLES = {"log4j", "python", "jquery", "oracle-database", "tomcat"}


def fetch(product, source=None):
    if source is not None:
        data = json.loads((source / f"{product}.json").read_text(encoding="utf-8"))
        if not isinstance(data, list) or not data:
            raise RuntimeError(f"{product}: archivo vacío o inesperado")
        return data

    request = urllib.request.Request(API.format(product), headers={"Accept": "application/json"})
    with urllib.request.urlopen(request, timeout=30) as response:
        if response.status != 200:
            raise RuntimeError(f"{product}: HTTP {response.status}")
        data = json.loads(response.read().decode("utf-8"))
    if not isinstance(data, list) or not data:
        raise RuntimeError(f"{product}: respuesta vacía o inesperada")
    return data


def date_or_none(value):
    return value if isinstance(value, str) else None


def keep(product, cycle):
    if product in KEEP:
        return cycle["cycle"] in KEEP[product]
    if product == "ubuntu":
        return bool(cycle.get("lts")) and cycle.get("releaseDate", "") >= "2014"
    return product in ALL_CYCLES or cycle.get("releaseDate", "9999") >= "2012"


def cycles_of(product, raw):
    cycles = []
    for c in raw:
        if not keep(product, c):
            continue
        entry = {"cycle": str(c["cycle"])}
        label = LABELS.get(product, {}).get(entry["cycle"])
        if label:
            entry["label"] = label
        entry["releaseDate"] = c.get("releaseDate")
        if date_or_none(c.get("support")):
            entry["supportEnds"] = c["support"]
        if date_or_none(c.get("eol")):
            entry["eol"] = c["eol"]
        elif c.get("eol") is True:
            entry["discontinued"] = True
        if date_or_none(c.get("extendedSupport")):
            entry["extendedSupportEnds"] = c["extendedSupport"]
        if c.get("lts"):
            entry["lts"] = True
        cycles.append(entry)
    return cycles


def main(argv):
    source = Path(argv[argv.index("--from") + 1]) if "--from" in argv else None
    products = []
    for key, name, kind in PRODUCTS:
        products.append({
            "key": key,
            "name": name,
            "kind": kind,
            "sourceUrl": f"https://endoflife.date/{key}",
            "cycles": cycles_of(key, fetch(key, source)),
        })

    catalog = {
        "version": "eol-catalog-v1",
        "description": (
            "End-of-life snapshot for the Code screen. Every date comes from the endoflife.date API "
            "(https://endoflife.date/docs/api), an open, community-maintained catalog under the MIT license. "
            "'eol' is the end of free security support; 'supportEnds' the end of active support; "
            "'extendedSupportEnds' the end of paid extended support, shown but not used as the end of life; "
            "'discontinued' marks a cycle that is no longer supported and has no published date. "
            "The GitHub Enterprise integration will refresh this snapshot on a schedule (ADR 0026)."
        ),
        "source": "endoflife.date",
        "sourceUrl": "https://endoflife.date",
        "retrievedOn": datetime.date.today().isoformat(),
        "products": products,
    }

    OUT.write_text(json.dumps(catalog, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    cycles = sum(len(p["cycles"]) for p in products)
    print(f"{OUT}: {len(products)} productos, {cycles} ciclos, retrievedOn {catalog['retrievedOn']}")


if __name__ == "__main__":
    try:
        main(sys.argv[1:])
    except Exception as error:  # noqa: BLE001 - un error de red o de formato se informa y sale
        print(f"No se pudo regenerar el catálogo: {error}", file=sys.stderr)
        sys.exit(1)
