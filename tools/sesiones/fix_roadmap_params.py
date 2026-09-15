import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# 1. Los umbrales son parametros de regla: viven en el contrato, no en el codigo.
edit(os.path.join(root, r"contracts\roadmap-v1.json"), [
    ('''  "coherenceRules": [''',
     '''  "parameters": {
    "healthyScore": 85,
    "significantUserBase": 500,
    "_comment": "healthyScore usa el piso del tramo Solido que muestra la interfaz, para que la alerta hable el mismo idioma que el resto de la aplicacion."
  },

  "coherenceRules": ['''),
])

# 2. El catalogo los expone; el planner los lee de ahi.
edit(os.path.join(root, r"src\shared\Modernization.Roadmap\RoadmapCatalog.cs"), [
    ('''    public required IReadOnlyList<CoherenceRuleDefinition> CoherenceRules { get; init; }''',
     '''    public required IReadOnlyList<CoherenceRuleDefinition> CoherenceRules { get; init; }

    /// <summary>Umbrales de las reglas de coherencia. Datos, no constantes.</summary>
    public required RoadmapParameters Parameters { get; init; }'''),

    ('''public sealed record DispositionDefinition''',
     '''/// <summary>
/// Umbrales que usan las reglas de coherencia. Cambiar uno es publicar un
/// catalogo nuevo, no recompilar.
/// </summary>
public sealed record RoadmapParameters
{
    /// <summary>
    /// Score a partir del cual reconstruir desde cero pide justificacion. Coincide
    /// con el piso del tramo Solido que muestra la interfaz.
    /// </summary>
    public required decimal HealthyScore { get; init; }

    /// <summary>Usuarios a partir de los cuales una baja es un proyecto de migracion.</summary>
    public required int SignificantUserBase { get; init; }
}

public sealed record DispositionDefinition'''),

    ('''        if (CoherenceRules.Count == 0)
        {
            throw new RoadmapException("El catalogo debe declarar al menos una regla de coherencia.");
        }''',
     '''        if (CoherenceRules.Count == 0)
        {
            throw new RoadmapException("El catalogo debe declarar al menos una regla de coherencia.");
        }

        if (Parameters.HealthyScore is < 20m or > 100m)
        {
            throw new RoadmapException(
                $"'healthyScore' debe estar dentro de la escala 20-100; se declaro {Parameters.HealthyScore}.");
        }

        if (Parameters.SignificantUserBase < 0)
        {
            throw new RoadmapException("'significantUserBase' no puede ser negativo.");
        }'''),
])

edit(os.path.join(root, r"src\shared\Modernization.Roadmap\RoadmapPlanner.cs"), [
    ('''    /// <summary>Umbral de usuarios que convierte una baja en un proyecto de migracion.</summary>
    private const int SignificantUserBase = 500;

    /// <summary>Score a partir del cual reconstruir desde cero pide justificacion.</summary>
    private const decimal HealthyScore = 80m;

''', ''),

    ("if (snapshot.UserCount >= SignificantUserBase)",
     "if (snapshot.UserCount >= catalog.Parameters.SignificantUserBase)"),

    ("if (snapshot.Roadmap == RoadmapType.Reimagine && baseline.EffectiveScore >= HealthyScore)",
     "if (snapshot.Roadmap == RoadmapType.Reimagine\n            && baseline.EffectiveScore >= catalog.Parameters.HealthyScore)"),

    ('$"Score actual {baseline.EffectiveScore:0.00}, por encima de {HealthyScore:0}.");',
     '$"Score actual {baseline.EffectiveScore:0.00}, por encima de {catalog.Parameters.HealthyScore:0}.");'),
])

# 3. El test usa una aplicacion que de verdad esta sana.
edit(os.path.join(root, r"tests\Modernization.Scoring.Engine.Tests\RoadmapTests.cs"), [
    ('''    [Fact]
    public void ReimagineOnAHealthyApplicationIsQuestioned()
    {
        var snapshot = GoldenFixtures.Eai10969() with
        {
            Disposition = ApplicationDisposition.Invest,
            Roadmap = RoadmapType.Reimagine,
        };''',

     '''    [Fact]
    public void ReimagineOnAHealthyApplicationIsQuestioned()
    {
        // Kenna bajo, sin flaws abiertos y OSS alto: score 96,81, muy por encima
        // del umbral de 85 que el catalogo declara como aplicacion sana.
        var snapshot = GoldenFixtures.Eai10969() with
        {
            Disposition = ApplicationDisposition.Invest,
            Roadmap = RoadmapType.Reimagine,
            KennaScore = 100,
            ScaFlaws = new ScaFlawCounts { Critical = 0, High = 0 },
            OpenSourceScore = 90m,
        };'''),

    ('''    [Fact]
    public void CatalogDeclaresEverySevenRoadmapsWithMilestones()
    {''',
     '''    [Fact]
    public void ParametersAreDataNotConstants()
    {
        Assert.Equal(85m, _catalog.Parameters.HealthyScore);
        Assert.Equal(500, _catalog.Parameters.SignificantUserBase);
    }

    [Fact]
    public void CatalogDeclaresEverySevenRoadmapsWithMilestones()
    {'''),

    ('''        const string Json = """
        {
          "version": "roto",
          "dispositions": [ { "key": "Tolerate", "name": "Tolerate", "definition": "x" } ],
          "roadmaps": [],
          "coherenceRules": [ { "key": "r", "severity": "Info", "title": "t", "detail": "d" } ]
        }
        """;''',
     '''        const string Json = """
        {
          "version": "roto",
          "parameters": { "healthyScore": 85, "significantUserBase": 500 },
          "dispositions": [ { "key": "Tolerate", "name": "Tolerate", "definition": "x" } ],
          "roadmaps": [],
          "coherenceRules": [ { "key": "r", "severity": "Info", "title": "t", "detail": "d" } ]
        }
        """;'''),
])
