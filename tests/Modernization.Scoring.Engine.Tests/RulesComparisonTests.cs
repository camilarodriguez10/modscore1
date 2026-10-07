using Modernization.Rules;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Comparar dos juegos de reglas sobre una aplicacion: la pregunta de antes de publicar.
/// </summary>
public sealed class RulesComparisonTests
{
    [Fact]
    public void AnApplicationComparedWithTheSameRulesChangesNothing()
    {
        var model = RulesModel.BuiltIn;

        var comparison = RulesComparison.Compare(GoldenFixtures.Eai14381(), model, model);

        Assert.Equal(80.70m, comparison.ScoreFrom);
        Assert.Equal(80.70m, comparison.ScoreTo);
        Assert.Equal(0m, comparison.Delta);
        Assert.Equal(model.Ruleset.Metrics.Count, comparison.Metrics.Count);
        Assert.All(comparison.Metrics, m => Assert.False(m.Changed));
        Assert.Empty(RulesComparison.Changes(model, model));
    }

    /// <summary>
    /// El caso calculado a mano en la Fase 12: EAI 10969 con Kenna en 5 y MFA en 10 pasa
    /// de 80,26 a 82,66 (numerador 62,659 sobre 80, con OSS y Raw Tech en su valor bruto).
    /// </summary>
    [Fact]
    public void MovingWeightShowsUpMetricByMetricAndInTheScore()
    {
        var edit = RulesEditor.Apply(
            RulesModel.BuiltIn,
            new RulesDraft([new MetricDraft("kenna", 5m), new MetricDraft("mfa", 10m)]));

        Assert.True(edit.IsValid);

        var comparison = RulesComparison.Compare(GoldenFixtures.Eai10969(), RulesModel.BuiltIn, edit.Candidate!);

        Assert.Equal(80.26m, comparison.ScoreFrom);
        Assert.Equal(82.66m, comparison.ScoreTo);
        Assert.Equal(2.40m, comparison.Delta);

        var kenna = comparison.Metrics.Single(m => m.Key == "kenna");
        Assert.Equal((8m, 5m), (kenna.WeightFrom, kenna.WeightTo));
        Assert.Equal(kenna.MappedFrom, kenna.MappedTo);
        Assert.True(kenna.Changed);

        var risk = comparison.Pillars.Single(p => p.Key == "risk");
        Assert.Equal((40m, 40m), (risk.AvailableFrom, risk.AvailableTo));

        Assert.All(comparison.Metrics.Where(m => m.Key is not ("kenna" or "mfa")), m => Assert.False(m.Changed));
        Assert.Equal(2, RulesComparison.Changes(RulesModel.BuiltIn, edit.Candidate!).Count);
    }

    [Fact]
    public void ADataPointOutsideEveryBandLeavesThatSideWithoutAScore()
    {
        // 2,45 cae en el hueco del BRD entre 2,4 y 2,5 (ADR 0004): no hay score, pero la
        // comparacion no falla y muestra igual las metricas con sus pesos.
        var application = GoldenFixtures.Eai14381() with { TechDebtScore = 2.45m };

        var comparison = RulesComparison.Compare(application, RulesModel.BuiltIn, RulesModel.BuiltIn);

        Assert.Null(comparison.ScoreFrom);
        Assert.Null(comparison.ScoreTo);
        Assert.Null(comparison.Delta);
        Assert.Equal(RulesModel.BuiltIn.Ruleset.Metrics.Count, comparison.Metrics.Count);
        Assert.Equal(10m, comparison.Metrics.Single(m => m.Key == "techDebt").WeightTo);
    }
}
