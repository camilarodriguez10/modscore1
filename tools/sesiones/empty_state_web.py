import io, os, re
root = r"C:\Users\kmiro\Downloads\mod score"

def read(rel):
    return io.open(os.path.join(root, rel), encoding='utf-8').read()

def write(rel, s):
    io.open(os.path.join(root, rel), 'w', encoding='utf-8', newline='').write(s)
    print("ok", rel)

# ------------------------------------------------------------- primitives
rel = r"src\web\src\components\primitives.tsx"
s = read(rel)

old_head = """import type { ReactNode } from 'react';
"""
new_head = """import type { ReactNode } from 'react';
import { Link } from 'react-router';
import { ApiError } from '../api/client';
"""
assert old_head in s
s = s.replace(old_head, new_head, 1)

old_error = """export function ErrorPanel({ title, detail }: { title: string; detail: string }) {"""
new_error = """/**
 * Estado inicial del adaptador de archivos: nadie ha publicado una carga todavia.
 *
 * No es un error — es la unica condicion del sistema que tiene una salida
 * concreta — asi que la pantalla ofrece esa salida en lugar de un panel rojo que
 * deja al usuario sin saber que hacer.
 */
export function NoDataPanel({ detail }: { detail: string }) {
  return (
    <div className="rounded-md border border-rule bg-surface-2 px-4 py-6">
      <p className="text-[13px] font-semibold">Todavia no hay ningun dato cargado</p>
      <p className="mt-1 max-w-[60ch] text-[13px] text-ink-2">{detail}</p>
      <Link
        to="/cargas"
        className="mt-3 inline-flex items-center rounded px-3 py-1.5 text-[13px] font-medium"
        style={{ background: 'var(--app-accent)', color: 'var(--app-on-accent)' }}
      >
        Ir a Cargas
      </Link>
    </div>
  );
}

/**
 * El error de una consulta, resuelto en una sola pieza: un 409 significa que
 * falta la primera carga y se ofrece la accion; cualquier otro estado es un
 * fallo y se muestra como tal.
 */
export function QueryError({ error, title }: { error: unknown; title: string }) {
  const api = error instanceof ApiError ? error : null;

  if (api?.status === 409) {
    return <NoDataPanel detail={api.message} />;
  }

  return (
    <ErrorPanel
      title={api?.title ?? title}
      detail={api?.message ?? 'Intenta de nuevo en unos segundos.'}
    />
  );
}

export function ErrorPanel({ title, detail }: { title: string; detail: string }) {"""
assert old_error in s
s = s.replace(old_error, new_error, 1)
write(rel, s)

# ------------------------------------------------------------- pantallas
pages = {
    "ExplainPage": "No se pudo cargar el desglose",
    "IngestionPage": "No se pudo cargar la pantalla",
    "LineagePage": "No se pudo cargar el linaje",
    "OverviewPage": "No se pudo cargar la aplicacion",
    "PortfolioPage": "No se pudo cargar el portafolio",
    "PortfolioRoadmapPage": "No se pudo cargar el roadmap del portafolio",
    "RoadmapPage": "No se pudo cargar el plan",
    "WhatIfPage": "No se pudo cargar el laboratorio",
}

for page, title in pages.items():
    rel = rf"src\web\src\routes\{page}.tsx"
    s = read(rel)

    old = f"""  if (error) {{
    const api = error instanceof ApiError ? error : null;
    return (
      <ErrorPanel
        title={{api?.title ?? '{title}'}}
        detail={{api?.message ?? 'Intenta de nuevo en unos segundos.'}}
      />
    );
  }}"""
    new = f"""  if (error) {{
    return <QueryError error={{error}} title="{title}" />;
  }}"""
    assert old in s, f"NO MATCH branch in {page}"
    s = s.replace(old, new, 1)

    # ErrorPanel y ApiError dejan de usarse en estas pantallas.
    s = s.replace("import { ApiError } from '../api/client';\n", "", 1)
    s = re.sub(r"\bErrorPanel\b", "QueryError", s)

    write(rel, s)

print("done")
