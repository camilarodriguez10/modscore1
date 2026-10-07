namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// El ruleset v1.3 con OSS y Raw Tech por bandas, como el BRD escrito. Fue el de fábrica hasta
/// el 2026-10-07 (ADR 0032): los dos casos dorados se validaron con él, y sigue probando que el
/// motor reproduce exactamente el ejemplo trabajado del BRD.
/// </summary>
internal static class TestRulesets
{
    private static readonly BandDefinition[] BrdBands =
    [
        new() { Min = 0m, Max = 59m, Score = 20m, Label = "0-59" },
        new() { Min = 60m, Max = 74m, Score = 60m, Label = "60-74" },
        new() { Min = 75m, Max = 100m, Score = 100m, Label = "75-100" },
    ];

    internal static Ruleset WithBrdBands { get; } = RulesetCatalog.Default with
    {
        Metrics = [.. RulesetCatalog.Default.Metrics.Select(m => m.Key is "oss" or "rawTech"
            ? m with { Mapping = new MappingDefinition { Kind = MappingKind.NumericBands, Bands = BrdBands } }
            : m)],
    };
}
