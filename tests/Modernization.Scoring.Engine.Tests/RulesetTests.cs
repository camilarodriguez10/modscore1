namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// El ruleset es el activo que va a cambiar con mas frecuencia: pesos, bandas y
/// versiones nuevas. Un ruleset incoherente produciria scores incorrectos sin
/// error visible, asi que se valida al cargarlo.
/// </summary>
public sealed class RulesetTests
{
    [Fact]
    public void DefaultRuleset_IsVersion13()
        => Assert.Equal("v1.3", RulesetCatalog.Default.Version);

    [Fact]
    public void DefaultRuleset_PillarWeightsSumOneHundred()
    {
        var total = RulesetCatalog.Default.Pillars.Sum(p => p.NominalWeight);

        Assert.Equal(100m, total);
    }

    [Theory]
    [InlineData("risk", 50)]
    [InlineData("architecture", 40)]
    [InlineData("stability", 10)]
    public void DefaultRuleset_MetricWeightsMatchTheirPillar(string pillarKey, decimal nominalWeight)
    {
        var ruleset = RulesetCatalog.Default;
        var sum = ruleset.Metrics.Where(m => m.Pillar == pillarKey).Sum(m => m.Weight);

        Assert.Equal(nominalWeight, sum);
    }

    [Fact]
    public void DefaultRuleset_EveryMetricDeclaresAKnownInput()
    {
        var unknown = RulesetCatalog.Default.Metrics
            .Where(m => !MetricExtractors.KnownInputs.Contains(m.Input))
            .Select(m => $"{m.Key} -> {m.Input}")
            .ToList();

        Assert.Empty(unknown);
    }

    [Fact]
    public void DefaultRuleset_EveryMetricDeclaresItsSource()
    {
        Assert.All(RulesetCatalog.Default.Metrics, metric =>
        {
            Assert.False(string.IsNullOrWhiteSpace(metric.Source.System), $"{metric.Key}: falta 'source.system'");
            Assert.False(string.IsNullOrWhiteSpace(metric.Source.Report), $"{metric.Key}: falta 'source.report'");
            Assert.False(string.IsNullOrWhiteSpace(metric.Source.Field), $"{metric.Key}: falta 'source.field'");
        });
    }

    [Fact]
    public void DefaultRuleset_NormalizesToTwentyOneHundred()
    {
        var normalization = RulesetCatalog.Default.Normalization;

        Assert.Equal(20m, normalization.Floor);
        Assert.Equal(80m, normalization.Range);
    }

    [Fact]
    public void DefaultRuleset_CompositeMetricsDeclareCategoryScores()
    {
        foreach (var key in new[] { "archValue", "productStrategy" })
        {
            var metric = RulesetCatalog.Default.Metrics.Single(m => m.Key == key);

            Assert.NotNull(metric.Composite);
            Assert.NotEmpty(metric.Composite.CategoryScores);
        }
    }

    [Fact]
    public void Load_WithUnknownVersion_Fails()
    {
        var ex = Assert.Throws<RulesetException>(() => RulesetCatalog.Load("v9.9"));

        Assert.Contains("v9.9", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsPillarWeightMismatch()
    {
        const string Json = """
        {
          "version": "test",
          "normalization": { "floor": 20, "range": 80 },
          "pillars": [ { "key": "risk", "name": "Risk", "nominalWeight": 50 } ],
          "metrics": [
            {
              "key": "kenna", "name": "Kenna", "pillar": "risk", "weight": 8, "input": "kennaScore",
              "source": { "system": "CMT", "report": "r", "field": "f" },
              "mapping": { "kind": "numericBands", "bands": [ { "min": 0, "max": 1000, "score": 100, "label": "all" } ] }
            }
          ]
        }
        """;

        var ex = Assert.Throws<RulesetException>(() => Ruleset.FromJson(Json));

        Assert.Contains("50", ex.Message, StringComparison.Ordinal);
        Assert.Contains("8", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsMetricPointingAtAMissingPillar()
    {
        const string Json = """
        {
          "version": "test",
          "normalization": { "floor": 20, "range": 80 },
          "pillars": [ { "key": "risk", "name": "Risk", "nominalWeight": 0 } ],
          "metrics": [
            {
              "key": "kenna", "name": "Kenna", "pillar": "typo", "weight": 0, "input": "kennaScore",
              "source": { "system": "CMT", "report": "r", "field": "f" },
              "mapping": { "kind": "numericBands", "bands": [ { "min": 0, "max": 1000, "score": 100, "label": "all" } ] }
            }
          ]
        }
        """;

        var ex = Assert.Throws<RulesetException>(() => Ruleset.FromJson(Json));

        Assert.Contains("typo", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromJson_RejectsMalformedJson()
        => Assert.Throws<RulesetException>(() => Ruleset.FromJson("{ not json"));
}
