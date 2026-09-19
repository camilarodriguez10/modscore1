using Modernization.Domain;
using Modernization.Insights;
using Modernization.Rules;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Las definiciones de las seis preguntas viajan con las reglas (ADR 0025): se editan con
/// el mismo borrador, se validan contra el catalogo, se guardan con la version y su
/// efecto se ve en los indicadores del dashboard, no en los scores.
/// </summary>
public sealed class RulesAnalyticsTests
{
    private static readonly DateOnly Start = new(2026, 9, 12);

    private static RulesEdit Edit(AnalyticsDraft analytics) => RulesEditor.Apply(RulesModel.BuiltIn, new RulesDraft(Analytics: analytics));

    private static AnalyticsDraft Values(params (string Key, decimal Value)[] values)
        => new(values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal));

    private static string Explain(RulesEdit edit) => string.Join(" | ", edit.Errors);

    [Fact]
    public void AValueInRangeChangesTheDefinitionAndIsListed()
    {
        var edit = Edit(Values(("currency.legacyBelow", 55.5m)));

        Assert.True(edit.IsValid, Explain(edit));
        Assert.Equal(55.5m, edit.Candidate!.Analytics.Currency.LegacyBelow);

        var change = Assert.Single(edit.Changes);
        Assert.Equal(NoticeCodes.ChangeAnalytics, change.Code);
        Assert.Equal("currency.legacyBelow", change.Args["parameter"]);
    }

    [Theory]
    [InlineData("currency.legacyBelow", 0)]
    [InlineData("currency.legacyBelow", 100.5)]
    [InlineData("currency.legacyBelow", 12.345)]
    [InlineData("risk.hotspotCount", 2.5)]
    [InlineData("candidates.minCriticality", 4)]
    [InlineData("concentration.topShare", 1.5)]
    public void AValueOutsideItsRangeIsRejectedByName(string key, double value)
    {
        var edit = Edit(Values((key, (decimal)value)));

        Assert.False(edit.IsValid);
        var error = Assert.Single(edit.Errors);
        Assert.Equal(NoticeCodes.RulesAnalyticsValue, error.Code);
        Assert.Equal(key, error.Args["parameter"]);
    }

    [Fact]
    public void AnUnknownValueIsReportedByPositionWithoutEchoingIt()
    {
        var edit = Edit(Values(("risk.moderateFrom", 45m), ("<script>", 1m)));

        var error = Assert.Single(edit.Errors);
        Assert.Equal(NoticeCodes.RulesAnalyticsUnknown, error.Code);
        Assert.Equal("2", error.Args["position"]);
        Assert.DoesNotContain(error.Args.Values, v => v.Contains("script", StringComparison.Ordinal));
    }

    /// <summary>Cada valor dentro de su rango, pero fuera de orden entre ellos.</summary>
    [Fact]
    public void LevelsOutOfOrderAreRejectedByTheRuleThatBreaks()
    {
        var edit = Edit(Values(("risk.highFrom", 90m)));

        Assert.False(edit.IsValid);
        var error = Assert.Single(edit.Errors);
        Assert.Equal(NoticeCodes.RulesAnalyticsInconsistent, error.Code);
        Assert.Equal("risk.levels", error.Args["rule"]);
    }

    [Fact]
    public void AnAreaCanBeAgreedAndGivenAnOwner()
    {
        var edit = Edit(new AnalyticsDraft(Areas: [new AnalyticsAreaDraft("currency", "agreed", "  CMT data owner  ")]));

        Assert.True(edit.IsValid, Explain(edit));
        Assert.Equal(new AnalyticsArea(AnalyticsArea.Agreed, "CMT data owner"), edit.Candidate!.Analytics.AreaOf("currency"));
        Assert.Equal(
            [NoticeCodes.ChangeAnalyticsStatus, NoticeCodes.ChangeAnalyticsOwner],
            edit.Changes.Select(c => c.Code));

        // Las demas areas no se tocan.
        Assert.Equal("Operations", edit.Candidate.Analytics.AreaOf("resilience").Owner);
    }

    [Theory]
    [InlineData("nowhere", "agreed", null, NoticeCodes.RulesAnalyticsArea)]
    [InlineData("risk", "final", null, NoticeCodes.RulesAnalyticsStatus)]
    [InlineData("risk", "agreed", "line one\nline two", NoticeCodes.RulesAnalyticsOwner)]
    public void AreasAreValidated(string key, string status, string? owner, string code)
    {
        var edit = Edit(new AnalyticsDraft(Areas: [new AnalyticsAreaDraft(key, status, owner)]));

        Assert.Equal(code, Assert.Single(edit.Errors).Code);
    }

    [Fact]
    public void AnOwnerLongerThanOneLineOfSixtyIsRejected()
    {
        var edit = Edit(new AnalyticsDraft(Areas: [new AnalyticsAreaDraft("risk", "agreed", new string('x', 61))]));

        Assert.Equal(NoticeCodes.RulesAnalyticsOwner, Assert.Single(edit.Errors).Code);
    }

    [Fact]
    public void TheDefinitionsTravelWithTheStoredVersion()
    {
        var candidate = Edit(Values(("risk.hotspotCount", 7m))).Candidate!;
        var published = candidate.WithInfo(new RulesVersionInfo
        {
            Id = "v1.4",
            BasedOn = "v1.3",
            PublishedAt = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero),
            PublishedBy = "tester",
            Reason = "More hotspots on the dashboard",
            Changes = [],
        });

        var read = RulesModel.FromStored(published.ToStored());

        Assert.Equal(7, read.Analytics.Risk.HotspotCount);
    }

    /// <summary>Una version publicada antes de que las definiciones viajaran con las reglas.</summary>
    [Fact]
    public void AVersionStoredWithoutDefinitionsUsesTheFactoryValues()
    {
        var stored = RulesModel.BuiltIn
            .WithInfo(new RulesVersionInfo
            {
                Id = "v1.4",
                PublishedAt = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero),
                PublishedBy = "tester",
                Reason = "Published before 2026-09-18",
                Changes = [],
            })
            .ToStored() with { Analytics = string.Empty };

        Assert.Equal(DashboardRules.Default, RulesModel.FromStored(stored).Analytics);
    }

    [Fact]
    public void DefinitionsNamingAPillarTheRulesetDoesNotHaveAreRejected()
    {
        var model = RulesModel.BuiltIn;
        var analytics = DashboardRules.Default with { Risk = DashboardRules.Default.Risk with { Pillar = "nowhere" } };

        Assert.Throws<RulesException>(() => new RulesModel(model.Info, model.Ruleset, model.Levers, model.Roadmap, analytics));
    }

    /// <summary>
    /// Una definicion no mueve ningun score: el impacto lo dice con cero movimientos y con
    /// el indicador que si cambia.
    /// </summary>
    [Fact]
    public void ADefinitionChangeMovesIndicatorsNotScores()
    {
        // 14381 y 10969 tienen RAW Tech Score; con el umbral en 100 las dos son legadas.
        var candidate = Edit(Values(("currency.legacyBelow", 100m))).Candidate!;

        var impact = RulesImpactSimulator.Simulate(
            [GoldenFixtures.Eai14381(), GoldenFixtures.Eai10969()],
            RulesModel.BuiltIn,
            candidate,
            Start);

        Assert.Equal(0, impact.Up + impact.Down);

        var currency = Assert.Single(impact.Indicators, i => i.Key == "currency");
        Assert.True(currency.CountAfter > currency.CountBefore, $"{currency.CountBefore} -> {currency.CountAfter}");
        Assert.Equal(
            ["health", "atRisk", "candidates", "currency", "resilience"],
            impact.Indicators.Select(i => i.Key));
    }
}
