import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:70]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# --- Dominio: los nombres MAPS son parte del modelo, no de la presentacion.
edit(os.path.join(root, r"src\shared\Modernization.Domain\ApplicationSnapshot.cs"), [
    ("""/// <summary>Roadmap MAPS.</summary>
public enum RoadmapType
{
    MaintainPatch,
    Rehost,
    MaintainEvolve,
    Refactor,
    Reimagine,
    Retire,
    Consolidate,
}""",
     """/// <summary>Roadmap MAPS.</summary>
public enum RoadmapType
{
    MaintainPatch,
    Rehost,
    MaintainEvolve,
    Refactor,
    Reimagine,
    Retire,
    Consolidate,
}

/// <summary>
/// Nombres MAPS tal como aparecen en el BRD. Son parte del modelo, no de la
/// presentacion: "Maintain (Patch)" es el nombre del roadmap, no una etiqueta
/// que cada capa pueda inventar por su cuenta.
/// </summary>
public static class MapsNames
{
    public static string Display(RoadmapType roadmap) => roadmap switch
    {
        RoadmapType.MaintainPatch => "Maintain (Patch)",
        RoadmapType.Rehost => "Rehost",
        RoadmapType.MaintainEvolve => "Maintain (Evolve)",
        RoadmapType.Refactor => "Refactor",
        RoadmapType.Reimagine => "Reimagine",
        RoadmapType.Retire => "Retire",
        RoadmapType.Consolidate => "Consolidate",
        _ => roadmap.ToString(),
    };

    public static string Display(ApplicationDisposition disposition) => disposition switch
    {
        ApplicationDisposition.Tolerate => "Tolerate",
        ApplicationDisposition.Invest => "Invest",
        ApplicationDisposition.Eliminate => "Eliminate",
        _ => disposition.ToString(),
    };
}"""),
])

# --- System API: emitir el nombre MAPS, no el del miembro del enum.
edit(os.path.join(root, r"src\system\Modernization.SystemApi\Contracts\SystemContracts.cs"), [
    ("""        s.Disposition?.ToString(),
        s.Roadmap?.ToString(),""",
     """        s.Disposition is { } d ? MapsNames.Display(d) : null,
        s.Roadmap is { } r ? MapsNames.Display(r) : null,"""),
])

# --- Motor: reutiliza el mismo mapa en lugar de repetirlo.
edit(os.path.join(root, r"src\shared\Modernization.Scoring.Engine\MetricExtractors.cs"), [
    ("""        var display = snapshot.Roadmap.Value switch
        {
            RoadmapType.MaintainPatch => "Maintain (Patch)",
            RoadmapType.Rehost => "Rehost",
            RoadmapType.MaintainEvolve => "Maintain (Evolve)",
            RoadmapType.Refactor => "Refactor",
            RoadmapType.Reimagine => "Reimagine",
            RoadmapType.Retire => "Retire",
            RoadmapType.Consolidate => "Consolidate",
            _ => snapshot.Roadmap.Value.ToString(),
        };

        return MetricInput.Present(display, display);""",
     """        var display = MapsNames.Display(snapshot.Roadmap.Value);

        return MetricInput.Present(display, display);"""),
])

# --- Front: el numero grande lleva token de texto; la marca de color va al lado.
edit(os.path.join(root, r"src\web\src\components\charts.tsx"), [
    ("""      <div className="flex items-end gap-3">
        <span
          className="text-[64px] leading-[0.9] font-semibold tracking-tight"
          style={{ color: bandColor(band) }}
        >
          {fmt(score)}
        </span>
        <BandChip band={band} />
      </div>""",
     """      {/* El numero lleva el token de texto, no el color de estado: en modo claro
          dos de los cuatro tramos quedan bajo 3:1 y una cifra de 64px ilegible
          seria el peor lugar para gastarlo. El chip de al lado carga el color. */}
      <div className="flex items-end gap-3">
        <span className="text-[64px] leading-[0.9] font-semibold tracking-tight text-ink">
          {fmt(score)}
        </span>
        <BandChip band={band} />
      </div>"""),
])
