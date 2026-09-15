import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:70]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# --- Backend: el modelo de vista ES el contrato. Omitir los nulos hace que el
#     cliente reciba `undefined` donde el tipo dice `null`, y esa diferencia
#     rompe cualquier comprobacion `=== null`.
edit(os.path.join(root, r"src\channel\Modernization.ChannelApi\Program.cs"), [
    ("""builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});""",
     """// Los nulos se serializan explicitamente. El modelo de vista es el contrato del
// front: omitir una propiedad nula hace que el cliente reciba `undefined` donde su
// tipo dice `null`, y esa diferencia rompe las comprobaciones en tiempo de render.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});"""),
])

# --- Frontend: `== null` cubre null y undefined. Defensa en profundidad frente a
#     cualquier fuente que vuelva a omitir nulos.
edit(os.path.join(root, r"src\web\src\routes\OverviewPage.tsx"), [
    ("""            <Field
              label="Usuarios"
              value={application.userCount === null ? '—' : application.userCount.toLocaleString('es-CL')}
            />
            <Field
              label="Aplicacion critica"
              value={application.criticalApplication === null ? '—' : application.criticalApplication ? 'Si' : 'No'}
            />""",
     """            <Field
              label="Usuarios"
              value={application.userCount == null ? '—' : application.userCount.toLocaleString('es-CL')}
            />
            <Field
              label="Aplicacion critica"
              value={
                application.criticalApplication == null
                  ? '—'
                  : application.criticalApplication
                    ? 'Si'
                    : 'No'
              }
            />"""),
    ("{pillar.score === null ? '—' : fmt(pillar.score)}", "{pillar.score == null ? '—' : fmt(pillar.score)}"),
    ("{metric.weightedScore === null ? '—' : fmt(metric.weightedScore)}",
     "{metric.weightedScore == null ? '—' : fmt(metric.weightedScore)}"),
    ("{metric.mappedScore === null ? '' : `${fmtScore(metric.mappedScore)} × ${fmt(metric.weight, 0)}%`}",
     "{metric.mappedScore == null ? '' : `${fmtScore(metric.mappedScore)} × ${fmt(metric.weight, 0)}%`}"),
])

edit(os.path.join(root, r"src\web\src\routes\ExplainPage.tsx"), [
    ("{metric.mappedScore === null ? '—' : fmtScore(metric.mappedScore)}",
     "{metric.mappedScore == null ? '—' : fmtScore(metric.mappedScore)}"),
    ("{metric.weightedScore === null ? '—' : fmt(metric.weightedScore)}",
     "{metric.weightedScore == null ? '—' : fmt(metric.weightedScore)}"),
])

edit(os.path.join(root, r"src\web\src\components\primitives.tsx"), [
    ("""export function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <Label>{label}</Label>
      <span className="text-[13px] text-ink">{value ?? '—'}</span>
    </div>
  );
}""",
     """export function Field({ label, value }: { label: string; value: ReactNode }) {
  // `??` cubre null y undefined: una fuente que omita la propiedad no puede
  // dejar el campo en blanco sin marca.
  const shown = value === null || value === undefined || value === '' ? '—' : value;

  return (
    <div className="flex flex-col gap-0.5">
      <Label>{label}</Label>
      <span className="text-[13px] text-ink">{shown}</span>
    </div>
  );
}"""),
])
