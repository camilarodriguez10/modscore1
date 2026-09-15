"""Verificacion en vivo de la Fase 10 contra la capa System levantada sobre una copia de data/history."""
import collections
import glob
import json
import os
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

SYSTEM = "http://localhost:5101"
TOKEN = "dev-only-service-token-not-for-any-real-environment"
HISTORY = os.path.join(os.path.dirname(os.path.abspath(__file__)), "live-history")

results = []


def check(name, ok, detail=""):
    results.append((ok, name, detail))
    print(("OK   " if ok else "FALLA") + f" {name}" + (f" — {detail}" if detail else ""))


def call(method, path, body=None, content_type=None):
    request = urllib.request.Request(SYSTEM + path, data=body, method=method)
    request.add_header("Authorization", "Bearer " + TOKEN)
    request.add_header("X-Portfolio-Scope", "*")
    request.add_header("X-Portfolio-User", "verificacion-fase-10")
    if content_type:
        request.add_header("Content-Type", content_type)
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        return error.code, None


def get(path):
    return call("GET", path)


def multipart(fields, files):
    boundary = uuid.uuid4().hex
    out = bytearray()
    for name, value in fields:
        out += (f"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n").encode()
    for filename, content in files:
        out += (f"--{boundary}\r\nContent-Disposition: form-data; name=\"files\"; filename=\"{filename}\"\r\n"
                "Content-Type: text/csv\r\n\r\n").encode()
        out += content + b"\r\n"
    out += f"--{boundary}--\r\n".encode()
    return bytes(out), "multipart/form-data; boundary=" + boundary


def all_applications():
    items, page = [], 1
    while True:
        status, body = get(f"/system/applications?page={page}&pageSize=200")
        assert status == 200, status
        items += body["items"]
        if len(items) >= body["totalCount"] or not body["items"]:
            return items
        page += 1


q = urllib.parse.quote

# ----------------------------------------------------------- filtros
apps = all_applications()
print(f"portafolio vivo: {len(apps)} aplicaciones")

for dimension, field in [("country", "country"), ("lob", "lob"), ("cio", "cio"),
                         ("disposition", "disposition"), ("roadmap", "roadmap")]:
    counts = collections.Counter(a[field] for a in apps if a[field])
    mismatches = []
    for key, expected in counts.items():
        _, body = get(f"/system/applications?{dimension}={q(key)}&pageSize=1")
        if body["totalCount"] != expected:
            mismatches.append(f"{key}: {body['totalCount']} != {expected}")
    check(f"cada valor de {dimension} filtra exactamente su conteo ({len(counts)} valores)",
          not mismatches, "; ".join(mismatches))

mexico = sum(1 for a in apps if a["country"] == "Mexico")
_, body = get("/system/applications?country=Mexico&pageSize=200")
check("clic en Mexico: solo Mexico y todas las de Mexico",
      body["totalCount"] == mexico and all(a["country"] == "Mexico" for a in body["items"]),
      f"{body['totalCount']} aplicaciones")

status, _ = get("/system/applications?band=Excelente")
check("tramo desconocido = 400, no un portafolio vacio", status == 400, str(status))

# ----------------------------------------------------------- roadmap por dimension
for grouping, field in [("country", "country"), ("cio", "cio"), ("lob", "lob"), ("roadmap", "roadmap")]:
    started = time.perf_counter()
    status, body = get(f"/system/roadmap/portfolio?groupBy={grouping}")
    elapsed = (time.perf_counter() - started) * 1000
    counts = collections.Counter(a[field] or "Sin asignar" for a in apps)
    groups = {g["key"]: g["count"] for g in body["groups"]}
    check(f"roadmap groupBy={grouping}: grupos reparten el portafolio y coinciden con el inventario",
          status == 200 and sum(groups.values()) == body["totalApplications"]
          and all(groups.get(k) == v for k, v in counts.items() if k in groups),
          f"{len(groups)} grupos · {elapsed:.0f} ms")

started = time.perf_counter()
status, body = get("/system/roadmap/portfolio?groupBy=application")
check("roadmap groupBy=application: una fila por aplicacion",
      status == 200 and len(body["groups"]) == body["totalApplications"],
      f"{len(body['groups'])} filas · {(time.perf_counter() - started) * 1000:.0f} ms")

status, body = get("/system/roadmap/portfolio?groupBy=country&country=Chile")
check("roadmap respeta los filtros", status == 200 and [g["key"] for g in body["groups"]] == ["Chile"],
      f"{body['totalApplications']} de Chile")

status, _ = get("/system/roadmap/portfolio?groupBy=planeta")
check("agrupacion desconocida = 400", status == 400, str(status))

# ----------------------------------------------------------- carga con nombres libres
_, before = get("/system/versions")
latest = sorted(d for d in os.listdir(HISTORY) if os.path.isdir(os.path.join(HISTORY, d)) and not d.startswith("."))[-1]
csvs = sorted(glob.glob(os.path.join(HISTORY, latest, "*.csv")))
files = [(f"Export Power BI ({i}).csv", open(path, "rb").read()) for i, path in enumerate(csvs)]
body, content_type = multipart([("label", "Verificacion fase 10 (nombres libres)")], files)

started = time.perf_counter()
status, upload = call("POST", "/system/ingestion/upload", body, content_type)
elapsed = time.perf_counter() - started
check("8 exports con nombres cualquiera se publican",
      status == 200 and upload["accepted"] and all(f["canonicalFileName"] for f in upload["files"]),
      f"{upload['applicationCount']} aplicaciones · {elapsed:.1f} s · "
      + ", ".join(f"{f['clientFileName']}→{f['canonicalFileName']}" for f in upload["files"][:2]) + ", …")

written = sorted(os.path.basename(p) for p in glob.glob(os.path.join(HISTORY, upload["version"]["id"], "*")))
check("en disco quedan los nombres canonicos, ninguno del cliente ni .part",
      not any(n.startswith("Export") or n.endswith(".part") for n in written), ", ".join(written))

_, after = get("/system/versions")
check("la carga es una version nueva", len(after) == len(before) + 1, f"{len(before)} → {len(after)}")

# ----------------------------------------------------------- ambiguo y asignacion
ambiguous = [("datos.csv", b"EAI,Country\n1001,Chile\n")]
body, content_type = multipart([("label", "ambigua")], ambiguous)
_, result = call("POST", "/system/ingestion/upload", body, content_type)
file = result["files"][0]
# La API omite las propiedades nulas: ausente equivale a null.
check("encabezado ambiguo: devuelve lo leido y las opciones",
      not result["accepted"] and file.get("canonicalFileName") is None
      and file.get("detectedColumns") == ["EAI", "Country"] and "apim_onboarded_eais.csv" in file.get("candidates", []),
      f"candidatas: {file.get('candidates')}")

body, content_type = multipart([("label", "asignada"), ("assign", "apim_onboarded_eais.csv")], ambiguous)
_, result = call("POST", "/system/ingestion/upload", body, content_type)
check("con la fuente asignada el archivo queda identificado",
      result["files"][0].get("canonicalFileName") == "apim_onboarded_eais.csv" and result["files"][0].get("error") is None)

body, content_type = multipart([("label", "ruta"), ("assign", "../../appsettings.json")], ambiguous)
_, result = call("POST", "/system/ingestion/upload", body, content_type)
check("asignacion con forma de ruta rechazada", not result["accepted"]
      and any("forma de nombre de archivo" in e for e in result["errors"]))

# ----------------------------------------------------------- linaje por version
oldest = sorted(after, key=lambda v: v["capturedAt"])[0]
status, sources = get(f"/system/sources?version={oldest['id']}")
check("linaje de una carga anterior", status == 200 and len(sources) == 8, f"{oldest['label']}: {len(sources)} fuentes")

status, _ = get("/system/sources?version=..%2F..%2Fsecrets")
check("version con forma de ruta = 404 (no esta entre las publicadas)", status == 404, str(status))

failed = [r for r in results if not r[0]]
print(f"\n{len(results) - len(failed)} de {len(results)} verificaciones en verde")
print("version publicada:", upload["version"]["id"], "·", upload["version"]["label"])
