import io, os
root = r"C:\Users\kmiro\Downloads\mod score\src\web"

def edit(rel, pairs):
    p = os.path.join(root, rel)
    s = io.open(p, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {rel}: {old[:70]!r}"
        s = s.replace(old, new)
    io.open(p, 'w', encoding='utf-8').write(s)
    print("ok", rel)

# 1. Token de texto sobre el acento: el acento se aclara en modo oscuro, asi que
#    el blanco deja de contrastar. El color del texto tiene que voltear con el.
edit("src/index.css", [
    ("  --app-accent: #0d5f6b;\n  --app-accent-wash: #e2eef0;\n\n  --app-risk: #2a78d6;",
     "  --app-accent: #0d5f6b;\n  --app-accent-wash: #e2eef0;\n  --app-on-accent: #ffffff;\n\n  --app-risk: #2a78d6;"),

    ("    --app-accent: #54b6c4;\n    --app-accent-wash: #14313a;\n\n    --app-risk: #3987e5;",
     "    --app-accent: #54b6c4;\n    --app-accent-wash: #14313a;\n    --app-on-accent: #0b1114;\n\n    --app-risk: #3987e5;"),

    ("  --app-accent: #54b6c4;\n  --app-accent-wash: #14313a;\n\n  --app-risk: #3987e5;",
     "  --app-accent: #54b6c4;\n  --app-accent-wash: #14313a;\n  --app-on-accent: #0b1114;\n\n  --app-risk: #3987e5;"),

    ("  --color-accent-wash: var(--app-accent-wash);",
     "  --color-accent-wash: var(--app-accent-wash);\n  --color-on-accent: var(--app-on-accent);"),
])

# 2. Un puntaje mapeado de 97,5 no puede mostrarse como 98: contradice la regla
#    que aparece a su lado.
edit("src/components/primitives.tsx", [
    ("""/** Dos decimales, coma decimal — igual que el dashboard original. */
export function fmt(value: number, decimals = 2): string {
  return value.toLocaleString('es-CL', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}""",
     """/** Dos decimales, coma decimal — igual que el dashboard original. */
export function fmt(value: number, decimals = 2): string {
  return value.toLocaleString('es-CL', {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}

/**
 * Puntaje mapeado. Los valores de banda son enteros (20, 60, 100) pero los
 * compuestos no lo son: mostrar 97,5 como "98" contradiria la regla que aparece
 * al lado. Se muestran decimales solo cuando existen.
 */
export function fmtScore(value: number): string {
  return Number.isInteger(value) ? fmt(value, 0) : fmt(value, 2);
}"""),
])

edit("src/routes/ExplainPage.tsx", [
    ("  Spinner,\n  fmt,\n  pillarColor,\n  pillarName,\n} from '../components/primitives';",
     "  Spinner,\n  fmt,\n  fmtScore,\n  pillarColor,\n  pillarName,\n} from '../components/primitives';"),

    ("""                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                    {metric.mappedScore === null ? '—' : fmt(metric.mappedScore, 0)}
                  </td>""",
     """                  <td className="tnum px-4 py-2.5 text-right font-mono text-ink">
                    {metric.mappedScore === null ? '—' : fmtScore(metric.mappedScore)}
                  </td>"""),

    ("""                <span className="tnum font-mono text-[12px] text-ink-3">
                  {metric.mappedScore === null ? '—' : fmt(metric.mappedScore, 0)}
                </span>""",
     """                <span className="tnum font-mono text-[12px] text-ink-3">
                  {metric.mappedScore === null ? '—' : fmtScore(metric.mappedScore)}
                </span>"""),
])

edit("src/routes/OverviewPage.tsx", [
    ("  Spinner,\n  fmt,\n  pillarColor,\n} from '../components/primitives';",
     "  Spinner,\n  fmt,\n  fmtScore,\n  pillarColor,\n} from '../components/primitives';"),

    ("{metric.mappedScore === null ? '' : `${fmt(metric.mappedScore, 0)} × ${fmt(metric.weight, 0)}%`}",
     "{metric.mappedScore === null ? '' : `${fmtScore(metric.mappedScore)} × ${fmt(metric.weight, 0)}%`}"),

    ('className="rounded-sm bg-accent px-3 py-1.5 text-[13px] font-medium text-white hover:opacity-90"',
     'className="rounded-sm bg-accent px-3 py-1.5 text-[13px] font-medium text-on-accent hover:opacity-90"'),
])

edit("src/App.tsx", [
    ('className="mt-5 w-full rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-white hover:opacity-90 disabled:opacity-60"',
     'className="mt-5 w-full rounded-sm bg-accent px-4 py-2 text-[13.5px] font-medium text-on-accent hover:opacity-90 disabled:opacity-60"'),
])
