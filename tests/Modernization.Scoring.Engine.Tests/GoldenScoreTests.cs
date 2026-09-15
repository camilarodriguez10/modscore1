using Modernization.Domain;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Reproduccion exacta de los dos casos documentados. Estos tests son el criterio
/// de aceptacion de la Fase 1: mientras pasen, el motor coincide con el dashboard.
/// </summary>
public sealed class GoldenScoreTests
{
    private readonly ScoringEngine _engine = new(RulesetCatalog.Default);

    [Fact]
    public void Eai14381_ReproducesBrdWorkedExample()
    {
        var result = _engine.Calculate(GoldenFixtures.Eai14381());

        Assert.Equal(44.75m, result.Numerator);
        Assert.Equal(60m, result.Denominator);
        Assert.Equal(79.67m, result.Score);
    }

    [Theory]
    [InlineData("risk", 17.00, 25, 74.40)]
    [InlineData("architecture", 21.75, 25, 89.60)]
    [InlineData("stability", 6.00, 10, 68.00)]
    public void Eai14381_ReproducesEachPillar(string pillarKey, double numerator, double availableWeight, double score)
    {
        var result = _engine.Calculate(GoldenFixtures.Eai14381());
        var pillar = result.Pillars.Single(p => p.Key == pillarKey);

        Assert.Equal((decimal)numerator, pillar.Numerator);
        Assert.Equal((decimal)availableWeight, pillar.AvailableWeight);
        Assert.Equal((decimal)score, pillar.Score);
    }

    [Fact]
    public void Eai10969_ReproducesDashboardV13()
    {
        var result = _engine.Calculate(GoldenFixtures.Eai10969());

        Assert.Equal(59.609m, result.Numerator);
        Assert.Equal(80m, result.Denominator);
        Assert.Equal(79.61m, result.Score);
    }

    [Theory]
    [InlineData("risk", 23.20, 40, 66.40)]
    [InlineData("architecture", 26.409, 30, 90.42)]
    [InlineData("stability", 10.00, 10, 100.00)]
    public void Eai10969_ReproducesEachPillar(string pillarKey, double numerator, double availableWeight, double score)
    {
        var result = _engine.Calculate(GoldenFixtures.Eai10969());
        var pillar = result.Pillars.Single(p => p.Key == pillarKey);

        Assert.Equal((decimal)numerator, pillar.Numerator);
        Assert.Equal((decimal)availableWeight, pillar.AvailableWeight);
        Assert.Equal((decimal)score, pillar.Score);
    }

    /// <summary>
    /// Los valores ponderados que el dashboard muestra por metrica, uno a uno.
    /// Si el score global coincidiera pero una metrica no, este test lo detecta.
    /// </summary>
    [Theory]
    [InlineData("kenna", 20, 1.60)]
    [InlineData("dr.inScope", 100, 7.00)]
    [InlineData("sca.high", 20, 1.60)]
    [InlineData("mfa", 100, 7.00)]
    [InlineData("oss", 60, 6.00)]
    [InlineData("rawTech", 100, 5.00)]
    [InlineData("api.native", 100, 5.00)]
    [InlineData("archValue", 75.72, 7.572)]
    [InlineData("productStrategy", 88.37, 8.837)]
    [InlineData("monitoring", 100, 5.00)]
    [InlineData("operational", 100, 5.00)]
    public void Eai10969_ReproducesEachWeightedMetric(string metricKey, double mappedScore, double weightedScore)
    {
        var result = _engine.Calculate(GoldenFixtures.Eai10969());
        var metric = result.Metrics.Single(m => m.MetricKey == metricKey);

        Assert.True(metric.IsIncluded, $"'{metricKey}' deberia estar incluida en el calculo.");
        Assert.Equal((decimal)mappedScore, metric.MappedScore);
        Assert.Equal((decimal)weightedScore, metric.WeightedScore);
    }

    [Theory]
    [InlineData("sca.critical")]
    [InlineData("techDebt")]
    public void Eai10969_ExcludesMetricsWithoutData(string metricKey)
    {
        var result = _engine.Calculate(GoldenFixtures.Eai10969());
        var metric = result.Metrics.Single(m => m.MetricKey == metricKey);

        Assert.False(metric.IsIncluded);
        Assert.Null(metric.MappedScore);
        Assert.Null(metric.WeightedScore);
        Assert.Equal(NoticeCodes.ValueNoData, metric.Display.Code);
        Assert.NotNull(metric.Exclusion);
    }

    /// <summary>
    /// Un pilar con metricas excluidas reduce su peso disponible. El dashboard lo
    /// muestra como dos numeros distintos (50 nominal, 40 disponible).
    /// </summary>
    [Fact]
    public void Eai10969_ReportsNominalAndAvailableWeightSeparately()
    {
        var result = _engine.Calculate(GoldenFixtures.Eai10969());

        var risk = result.Pillars.Single(p => p.Key == "risk");
        Assert.Equal(50m, risk.TotalWeight);
        Assert.Equal(40m, risk.AvailableWeight);

        var architecture = result.Pillars.Single(p => p.Key == "architecture");
        Assert.Equal(40m, architecture.TotalWeight);
        Assert.Equal(30m, architecture.AvailableWeight);
    }

    /// <summary>
    /// El motor es puro: la misma entrada produce la misma salida siempre.
    /// Sin esto, ni el what-if ni la auditoria son confiables.
    /// </summary>
    [Fact]
    public void Calculate_IsDeterministic()
    {
        var snapshot = GoldenFixtures.Eai10969();

        var first = _engine.Calculate(snapshot);
        var second = _engine.Calculate(snapshot);

        Assert.Equal(first.Score, second.Score);
        Assert.Equal(first.Numerator, second.Numerator);
        Assert.Equal(first.CalculatedAt, second.CalculatedAt);
    }

    /// <summary>Todo score calculado registra con que ruleset se produjo.</summary>
    [Fact]
    public void Calculate_StampsRulesetVersion()
    {
        var result = _engine.Calculate(GoldenFixtures.Eai10969());

        Assert.Equal("v1.3", result.RulesetVersion);
    }

    /// <summary>
    /// Cada metrica incluida explica su regla; cada metrica excluida explica su
    /// ausencia. Es lo que sostiene la pantalla "como se calculo".
    /// </summary>
    [Fact]
    public void Calculate_ExplainsEveryMetric()
    {
        var result = _engine.Calculate(GoldenFixtures.Eai10969());

        Assert.NotEmpty(result.Metrics);
        foreach (var metric in result.Metrics)
        {
            Assert.NotEmpty(metric.Name);
            Assert.NotEmpty(metric.Provenance.System);
            Assert.NotEmpty(metric.Provenance.Field);

            if (metric.IsIncluded)
            {
                Assert.NotEmpty(metric.RuleApplied);
            }
            else
            {
                Assert.NotNull(metric.Exclusion);
            }
        }
    }

    /// <summary>Un override manual no borra el calculo: convive con el.</summary>
    [Fact]
    public void Calculate_KeepsCalculatedScoreAlongsideOverride()
    {
        var scoreOverride = new ScoreOverride
        {
            Value = 85m,
            Reason = "Remediacion validada fuera de ciclo por AppSec",
            AppliedBy = "cre@example.com",
            AppliedAt = GoldenFixtures.CapturedAt,
        };

        var result = _engine.Calculate(GoldenFixtures.Eai10969(), scoreOverride);

        Assert.Equal(79.61m, result.Score);
        Assert.Equal(85m, result.EffectiveScore);
        Assert.NotNull(result.Override);
    }
}
