"""Fase 11, revision en vivo: plurales en ingles y tramos con nombre anterior en la URL."""
import io
import re

WEB = r"C:\Users\kmiro\Downloads\mod score\src\web\src"


def load(path):
    raw = io.open(path, encoding="utf-8", newline="").read()
    return raw, "\r\n" in raw


def save(path, text, crlf):
    if crlf:
        text = text.replace("\r\n", "\n").replace("\n", "\r\n")
    io.open(path, "w", encoding="utf-8", newline="").write(text)


def patch(rel, pairs, imports=()):
    path = WEB + "\\" + rel
    raw, crlf = load(path)
    s = raw.replace("\r\n", "\n")
    for old, new in pairs:
        n = s.count(old)
        assert n >= 1, f"{rel}: no encontrado: {old[:60]!r}"
        s = s.replace(old, new)
    for name, module in imports:
        pattern = re.compile(r"import \{([^}]*)\} from '" + re.escape(module) + r"';")
        m = pattern.search(s)
        if m:
            names = [x.strip() for x in m.group(1).split(",") if x.strip()]
            if name not in names:
                names.append(name)
                names.sort(key=str.lower)
                s = s[: m.start()] + "import { " + ", ".join(names) + " } from '" + module + "';" + s[m.end():]
        else:
            last = list(re.finditer(r"^import .*?;\n", s, re.M | re.S))[-1]
            s = s[: last.end()] + f"import {{ {name} }} from '{module}';\n" + s[last.end():]
    save(path, s, crlf)
    print("ok", rel)


# ------------------------------------------------------------------ catalogo
patch(
    r"i18n\notices.ts",
    [
        (
            "`${a.state} · ${num(a.users)} users`",
            "`${a.state} · ${num(a.users)} ${plural(a.users, 'user')}`",
        ),
        (
            "`${num(a.users)} users without multi-factor; the rehost leaves them unchanged.`",
            "`${num(a.users)} ${plural(a.users, 'user')} without multi-factor; the rehost leaves them unchanged.`",
        ),
        ("${num(a.weeks)} weeks", "${num(a.weeks)} ${plural(a.weeks, 'week')}"),
        (
            "`Upload published: ${num(a.files)} files, ${num(a.rows)} rows, ${num(a.applications)} applications recalculated.`",
            "`Upload published: ${num(a.files)} ${plural(a.files, 'file')}, ${num(a.rows)} ${plural(a.rows, 'row')}, ${num(a.applications)} ${plural(a.applications, 'application')} recalculated.`",
        ),
        (
            "// ------------------------------------------------------------------ bands\n",
            "// ----------------------------------------------------------------- counts\n\n"
            "/** \"1 application\", \"250 applications\": el conteo con su sustantivo concordado. */\n"
            "export function countOf(count: number, singular: string, many = `${singular}s`): string {\n"
            "  return `${numberFormat.format(count)} ${count === 1 ? singular : many}`;\n"
            "}\n\n"
            "// ------------------------------------------------------------------ bands\n",
        ),
        (
            "/** Nombre de una clave de agregado: la vacia es \"sin asignar\". */",
            "/**\n"
            " * Nombres de tramo de la Fase 10. Un enlace guardado entonces todavia puede\n"
            " * traerlos; el servidor los acepta, y aqui se vuelven codigo para que la barra\n"
            " * de filtros no muestre el nombre anterior.\n"
            " */\n"
            "const LEGACY_BANDS: Record<string, string> = {\n"
            "  critico: 'critical',\n"
            "  'en riesgo': 'at-risk',\n"
            "  aceptable: 'acceptable',\n"
            "  solido: 'solid',\n"
            "};\n\n"
            "/** El codigo de un tramo, aunque llegue con su nombre anterior. */\n"
            "export function bandCode(value: string): string {\n"
            "  return LEGACY_BANDS[value.trim().toLowerCase()] ?? value;\n"
            "}\n\n"
            "/** Nombre de una clave de agregado: la vacia es \"sin asignar\". */",
        ),
    ],
)

# ------------------------------------------------------------------ filtros
patch(
    "filters.tsx",
    [
        (
            "import { bandLabel } from './i18n/notices';",
            "import { bandCode, bandLabel } from './i18n/notices';",
        ),
        (
            "    if (value) filters[key] = value;\n",
            "    if (value) filters[key] = key === 'band' ? bandCode(value) : value;\n",
        ),
        (
            "    if (hasFilters) {\n      remember(search);\n      return;\n    }\n",
            "    if (hasFilters) {\n"
            "      // Un enlace de la Fase 10 trae el tramo por su nombre: la URL pasa al\n"
            "      // codigo, para que lo que se comparta desde aqui ya salga en ingles.\n"
            "      const band = params.get('band');\n"
            "      if (band && bandCode(band) !== band) {\n"
            "        const updated = new URLSearchParams(params);\n"
            "        updated.set('band', bandCode(band));\n"
            "        setParams(updated, { replace: true });\n"
            "      }\n\n"
            "      remember(search);\n"
            "      return;\n"
            "    }\n",
        ),
    ],
)

# ------------------------------------------------------------------ pantallas
N = ("countOf", "../i18n/notices")

patch(r"routes\PortfolioPage.tsx", [("{data.totalApplications} applications", "{countOf(data.totalApplications, 'application')}")], [N])
patch(
    r"routes\PortfolioRoadmapPage.tsx",
    [("{data.totalApplications} applications with a plan", "{countOf(data.totalApplications, 'application')} with a plan")],
    [N],
)
patch(r"routes\IngestionPage.tsx", [("{version.applicationCount} applications", "{countOf(version.applicationCount, 'application')}")], [N])
patch(
    r"routes\LineagePage.tsx",
    [
        ("{data.sources.length} sources in", "{countOf(data.sources.length, 'source')} in"),
        ("the {data.totalApplications} applications that", "the {countOf(data.totalApplications, 'application')} that"),
        (
            "{source.matchedApplications} of {data.totalApplications} applications",
            "{source.matchedApplications} of {countOf(data.totalApplications, 'application')}",
        ),
    ],
    [N],
)
patch(
    r"routes\RoadmapPage.tsx",
    [
        (
            "{data.milestones.length} milestones · {data.totalWeeks} weeks",
            "{countOf(data.milestones.length, 'milestone')} · {countOf(data.totalWeeks, 'week')}",
        ),
        ("{data.totalWeeks} weeks</p>", "{countOf(data.totalWeeks, 'week')}</p>"),
        ("${milestone.weeks} weeks`", "${countOf(milestone.weeks, 'week')}`"),
    ],
    [N],
)
patch(
    r"components\charts.tsx",
    [
        ("${band.count} applications,", "${countOf(band.count, 'application')},"),
        ("${group.count} applications,", "${countOf(group.count, 'application')},"),
        ("{group.count} apps</span>", "{countOf(group.count, 'app')}</span>"),
    ],
    [N],
)
