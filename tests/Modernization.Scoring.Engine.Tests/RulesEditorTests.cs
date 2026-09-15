using Modernization.Domain;
using Modernization.Roadmap;
using Modernization.Rules;
using Modernization.WhatIf;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Fase 12: un borrador de reglas se valida entero, reporta cada problema con su
/// codigo y, si es valido, produce un modelo que puntua con el mismo motor.
/// </summary>
public sealed class RulesEditorTests
{
    private static RulesEdit Edit(RulesDraft draft) => RulesEditor.Apply(RulesModel.BuiltIn, draft);

    private static RulesDraft Weights(params (string Key, decimal Weight)[] weights)
        => new(Metrics: [.. weights.Select(w => new MetricDraft(w.Key, w.Weight))]);

    private static RulesDraft Oss(IReadOnlyList<BandDraft> bands) => new(Metrics: [new MetricDraft("oss", 10, bands)]);

    private static List<string> Codes(RulesEdit edit) => [.. edit.Errors.Select(e => e.Code)];

    private static string Explain(RulesEdit edit) => string.Join(" | ", edit.Errors);

    /// <summary>El borrador que arma la pantalla a partir del documento, sin tocar nada.</summary>
    internal static RulesDraft DraftOf(RulesDocument document) => new(
        [.. document.Metrics.Select(m => new MetricDraft(m.Key, m.Weight, m.Bands, m.Cases, m.CategoryScores))],
        [.. document.Levers.Select(l => new LeverDraft(l.Key, l.Enabled, l.DefaultParameter))],
        new RoadmapDraft(
            document.Roadmap.HealthyScore,
            document.Roadmap.SignificantUserBase,
            [.. document.Roadmap.Roadmaps.Select(r => new RoadmapPathDraft(
                r.Key,
                r.Levers,
                [.. r.Milestones.Select(m => m.Weeks)]))]));

    [Fact]
    public void AnEmptyDraftIsValidAndChangesNothing()
    {
        var edit = Edit(new RulesDraft());

        Assert.True(edit.IsValid, Explain(edit));
        Assert.Empty(edit.Changes);
    }

    /// <summary>
    /// La pantalla reenvia el documento completo. Sin tocar ningun campo, eso no puede
    /// aparecer como cambio: si apareciera, cada publicacion arrastraria ruido.
    /// </summary>
    [Fact]
    public void ResendingTheWholeDocumentChangesNothing()
    {
        var edit = Edit(DraftOf(RulesDocument.From(RulesModel.BuiltIn)));

        Assert.True(edit.IsValid, Explain(edit));
        Assert.Empty(edit.Changes);
    }

    /// <summary>
    /// EAI 10969: Kenna puntua 20 y MFA 100, las dos con dato. Pasar 3 puntos de peso de
    /// Kenna a MFA lleva el numerador de 59,609 a 59,609 - 3x0,20 + 3x1,00 = 62,009, con
    /// el mismo peso disponible de 80: 20 + 62,009 / 80 x 80 = 82,01.
    /// </summary>
    [Fact]
    public void MovingWeightFromKennaToMfaMatchesTheHandCalculation()
    {
        var edit = Edit(Weights(("kenna", 5), ("mfa", 10)));

        Assert.True(edit.IsValid, Explain(edit));

        var result = edit.Candidate!.Engine.Calculate(GoldenFixtures.Eai10969());

        Assert.Equal(62.009m, result.Numerator);
        Assert.Equal(80m, result.Denominator);
        Assert.Equal(82.01m, result.Score);

        Assert.Contains(edit.Changes, c =>
            c.Code == NoticeCodes.ChangeWeight
            && c.Args["metric"] == "MFA"
            && c.Args["from"] == "7"
            && c.Args["to"] == "10");
    }

    /// <summary>El peso de un pilar no se edita: es la suma de sus metricas.</summary>
    [Fact]
    public void PillarWeightsFollowTheirMetrics()
    {
        var edit = Edit(Weights(("oss", 5), ("operational", 10)));

        Assert.True(edit.IsValid, Explain(edit));

        var pillars = edit.Candidate!.Ruleset.Pillars.ToDictionary(p => p.Key, p => p.NominalWeight);

        Assert.Equal(45m, pillars["risk"]);
        Assert.Equal(40m, pillars["architecture"]);
        Assert.Equal(15m, pillars["stability"]);
    }

    [Fact]
    public void WeightsMustAddUpToOneHundred()
    {
        var edit = Edit(Weights(("mfa", 10)));

        var total = Assert.Single(edit.Errors, e => e.Code == NoticeCodes.RulesWeightTotal);

        Assert.Equal("103", total.Args["total"]);
        Assert.Null(edit.Candidate);
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(100.5)]
    [InlineData(7.125)]
    public void AWeightOutsideTheScaleOrWithTooManyDecimalsIsRejected(double weight)
        => Assert.Contains(NoticeCodes.RulesWeightInvalid, Codes(Edit(Weights(("mfa", (decimal)weight)))));

    /// <summary>Lo que no existe se senala por posicion: no se le devuelve a nadie el texto que envio.</summary>
    [Fact]
    public void AnUnknownMetricIsReportedByPositionWithoutEchoingWhatWasSent()
    {
        var edit = Edit(new RulesDraft(Metrics: [new MetricDraft("kenna", 8), new MetricDraft("<img src=x>", 5)]));

        var error = Assert.Single(edit.Errors);

        Assert.Equal(NoticeCodes.RulesMetricUnknown, error.Code);
        Assert.Equal("2", error.Args["position"]);
        Assert.DoesNotContain(edit.Errors.SelectMany(e => e.Args.Values), value => value.Contains('<', StringComparison.Ordinal));
    }

    [Fact]
    public void AMetricListedTwiceIsRejected()
        => Assert.Contains(NoticeCodes.RulesDuplicate, Codes(Edit(Weights(("kenna", 8), ("kenna", 8)))));

    /// <summary>Un valor que cae en dos bandas puntuaria segun el orden de la lista.</summary>
    [Fact]
    public void OverlappingBandsAreRejected()
    {
        var edit = Edit(Oss([new(0, 59, 20, "0-59"), new(50, 74, 60, "50-74"), new(75, 100, 100, "75-100")]));

        Assert.Contains(NoticeCodes.RulesBandOrder, Codes(edit));
    }

    [Fact]
    public void BandBoundsLabelsAndCountAreValidated()
    {
        Assert.Contains(
            NoticeCodes.RulesBandBounds,
            Codes(Edit(Oss([new(59, 0, 20, "low"), new(60, 100, 100, "high")]))));

        Assert.Equal(
            2,
            Edit(Oss([new(0, 59, 20, " "), new(60, 100, 100, "hi")])).Errors.Count(e => e.Code == NoticeCodes.RulesBandLabel));

        Assert.Contains(
            NoticeCodes.RulesBandCount,
            Codes(Edit(Oss([.. Enumerable.Range(0, 11).Select(i => new BandDraft(i * 10, (i * 10) + 5, 50, "band"))]))));
    }

    [Fact]
    public void BandsOnlyApplyToNumericMetrics()
    {
        var edit = Edit(new RulesDraft(Metrics: [new MetricDraft("mfa", 7, Bands: [new(0, 100, 100, "all")])]));

        Assert.Contains(NoticeCodes.RulesKindMismatch, Codes(edit));
    }

    /// <summary>Las claves de un caso son lo que produce el extractor: una nueva nunca coincidiria.</summary>
    [Fact]
    public void CaseKeysCannotBeAddedOrRemoved()
    {
        var missing = new Dictionary<string, decimal>
        {
            ["Yes (with users)"] = 100,
            ["Yes (with no users)"] = 100,
            ["No (with users)"] = 20,
        };

        var renamed = new Dictionary<string, decimal>(missing) { ["Maybe"] = 60 };

        Assert.Contains(NoticeCodes.RulesCaseKeys, Codes(Edit(new RulesDraft(Metrics: [new MetricDraft("mfa", 7, Cases: missing)]))));
        Assert.Contains(NoticeCodes.RulesCaseKeys, Codes(Edit(new RulesDraft(Metrics: [new MetricDraft("mfa", 7, Cases: renamed)]))));
    }

    [Fact]
    public void ACaseScoreChangeIsListedCaseByCase()
    {
        var cases = new Dictionary<string, decimal>
        {
            ["Yes (with users)"] = 100,
            ["Yes (with no users)"] = 100,
            ["No (with users)"] = 40,
            ["No (with no users)"] = 100,
        };

        var edit = Edit(new RulesDraft(Metrics: [new MetricDraft("mfa", 7, Cases: cases)]));

        Assert.True(edit.IsValid, Explain(edit));

        var change = Assert.Single(edit.Changes);

        Assert.Equal(NoticeCodes.ChangeCase, change.Code);
        Assert.Equal("No (with users)", change.Args["case"]);
        Assert.Equal("20", change.Args["from"]);
        Assert.Equal("40", change.Args["to"]);
    }

    [Fact]
    public void CategoryScoresKeepTheirKeys()
    {
        var scores = new Dictionary<string, decimal> { ["modern"] = 100, ["heritage"] = 60, ["obsolete"] = 20, ["legacy"] = 20 };

        var edit = Edit(new RulesDraft(Metrics: [new MetricDraft("archValue", 10, CategoryScores: scores)]));

        Assert.Contains(NoticeCodes.RulesCategoryKeys, Codes(edit));
    }

    [Fact]
    public void LeverQuantitiesAreValidated()
    {
        Assert.Contains(NoticeCodes.RulesLeverParameter, Codes(Edit(new RulesDraft(Levers: [new LeverDraft("archValue.modernize", true, 0)]))));
        Assert.Contains(NoticeCodes.RulesLeverParameter, Codes(Edit(new RulesDraft(Levers: [new LeverDraft("mfa.enable", true, 3)]))));
        Assert.Contains(NoticeCodes.RulesLeverUnknown, Codes(Edit(new RulesDraft(Levers: [new LeverDraft("mfa.disable", true)]))));
    }

    /// <summary>
    /// Apagar una palanca que un roadmap entrega es cambiar ese roadmap. Dejarlo pasar
    /// en silencio haria que el plan futuro proyecte con algo que el laboratorio ya no
    /// ofrece.
    /// </summary>
    [Fact]
    public void TurningOffALeverThatARoadmapDeliversIsReportedAsARoadmapProblem()
    {
        var edit = Edit(new RulesDraft(Levers: [new LeverDraft("mfa.enable", false)]));

        var error = Assert.Single(edit.Errors);

        Assert.Equal(NoticeCodes.RulesRoadmapLever, error.Code);
        Assert.Equal("Reimagine", error.Args["roadmap"]);
        Assert.Equal("Enable MFA", error.Args["lever"]);
    }

    [Fact]
    public void ALeverTurnedOffLeavesTheLabTheCeilingAndTheScenarios()
    {
        var reimagine = RoadmapCatalog.Default.Require(RoadmapType.Reimagine);

        var edit = Edit(new RulesDraft(
            Levers: [new LeverDraft("mfa.enable", false)],
            Roadmap: new RoadmapDraft(85, 500, [new RoadmapPathDraft(
                "Reimagine",
                [.. reimagine.Levers.Where(l => l != "mfa.enable")],
                [.. reimagine.Milestones.Select(m => m.Weeks)])])));

        Assert.True(edit.IsValid, Explain(edit));

        var whatIf = edit.Candidate!.WhatIf;

        Assert.DoesNotContain(whatIf.EvaluateAllLevers(GoldenFixtures.Eai10969()).Levers, l => l.Key == "mfa.enable");

        var ex = Assert.Throws<WhatIfException>(() => whatIf.Evaluate(GoldenFixtures.Eai10969(), [new LeverSelection("mfa.enable")]));
        Assert.Equal("unknown-lever", ex.Code);

        Assert.Contains(edit.Changes, c => c.Code == NoticeCodes.ChangeLeverEnabled && c.Args["enabled"] == "false");
        Assert.Contains(edit.Changes, c => c.Code == NoticeCodes.ChangeRoadmapLevers && c.Args["removed"] == "Enable MFA");
    }

    [Fact]
    public void RoadmapParametersAndMilestonesAreValidated()
    {
        var refactor = RoadmapCatalog.Default.Require(RoadmapType.Refactor);

        RulesEdit With(decimal healthy, int users, RoadmapPathDraft? path = null)
            => Edit(new RulesDraft(Roadmap: new RoadmapDraft(healthy, users, path is null ? null : [path])));

        Assert.Contains(NoticeCodes.RulesHealthyScore, Codes(With(10, 500)));
        Assert.Contains(NoticeCodes.RulesUserBase, Codes(With(85, -1)));
        Assert.Contains(NoticeCodes.RulesMilestones, Codes(With(85, 500, new RoadmapPathDraft("Refactor", refactor.Levers, [4, 4]))));
        Assert.Contains(NoticeCodes.RulesMilestones, Codes(With(85, 500, new RoadmapPathDraft("Refactor", refactor.Levers, [0, 4, 16, 6, 8, 4]))));
        Assert.Contains(NoticeCodes.RulesRoadmapUnknown, Codes(With(85, 500, new RoadmapPathDraft("3", [], [1]))));
        Assert.Contains(NoticeCodes.RulesRoadmapLeaves, Codes(With(85, 500, new RoadmapPathDraft("Retire", ["mfa.enable"], [4, 4, 6, 3, 1]))));
    }

    [Fact]
    public void AMilestoneChangeIsListedAndLengthensThePlan()
    {
        var refactor = RoadmapCatalog.Default.Require(RoadmapType.Refactor);

        var edit = Edit(new RulesDraft(Roadmap: new RoadmapDraft(
            85,
            500,
            [new RoadmapPathDraft("Refactor", refactor.Levers, [6, 4, 16, 6, 8, 4])])));

        Assert.True(edit.IsValid, Explain(edit));
        Assert.Equal(refactor.TotalWeeks + 2, edit.Candidate!.Roadmap.Require(RoadmapType.Refactor).TotalWeeks);

        var change = Assert.Single(edit.Changes);

        Assert.Equal(NoticeCodes.ChangeMilestoneWeeks, change.Code);
        Assert.Equal("4", change.Args["from"]);
        Assert.Equal("6", change.Args["to"]);
    }

    [Theory]
    [InlineData("Rebalance risk weights", true)]
    [InlineData("   Rebalance risk weights   ", true)]
    [InlineData("too short", false)]
    [InlineData("Line one\nline two", false)]
    public void AReasonIsOneReadableLine(string reason, bool accepted)
        => Assert.Equal(accepted, RulesEditor.TryNormalizeReason(reason, out _));
}

/// <summary>
/// Las palancas que llevan una metrica a su mejor banda leen esa banda de las reglas:
/// editar la banda mueve el objetivo sin tocar codigo.
/// </summary>
public sealed class LeverTargetTests
{
    private static ApplicationSnapshot Apply(LeverCatalog catalog, string key, ApplicationSnapshot snapshot)
        => catalog.All.Single(l => l.Key == key).Apply(snapshot, null);

    /// <summary>Con las reglas de fabrica, los objetivos son los que eran constantes: 75, 339, 75 y 80.</summary>
    [Fact]
    public void FactoryTargetsAreTheTopBandsOfV13()
    {
        var catalog = LeverCatalog.Default;
        var low = GoldenFixtures.Eai10969() with { OpenSourceScore = 40m, KennaScore = 800, RawTechScore = 10m, OperationalScore = 50m };

        Assert.Equal(75m, Apply(catalog, "oss.improve", low).OpenSourceScore);
        Assert.Equal(339, Apply(catalog, "kenna.reduce", low).KennaScore);
        Assert.Equal(75m, Apply(catalog, "rawTech.improve", low).RawTechScore);
        Assert.Equal(80m, Apply(catalog, "operational.improve", low).OperationalScore);

        var missing = low with { OpenSourceScore = null, KennaScore = null };

        Assert.Equal(75m, Apply(catalog, "oss.improve", missing).OpenSourceScore);
        Assert.Equal(339, Apply(catalog, "kenna.reduce", missing).KennaScore);
    }

    /// <summary>Antes, la palanca fijaba 75 aunque la aplicacion tuviera 92: nunca empeora un valor.</summary>
    [Fact]
    public void AValueAlreadyInTheTopBandIsLeftAlone()
    {
        var high = GoldenFixtures.Eai10969() with { OpenSourceScore = 92m, KennaScore = 120 };

        Assert.Equal(92m, Apply(LeverCatalog.Default, "oss.improve", high).OpenSourceScore);
        Assert.Equal(120, Apply(LeverCatalog.Default, "kenna.reduce", high).KennaScore);
    }

    [Fact]
    public void EditingTheTopBandMovesTheLeverTarget()
    {
        var edit = RulesEditor.Apply(
            RulesModel.BuiltIn,
            new RulesDraft(Metrics: [new MetricDraft("oss", 10, [new(0, 59, 20, "0-59"), new(60, 79, 60, "60-79"), new(80, 100, 100, "80-100")])]));

        Assert.True(edit.IsValid, string.Join(" | ", edit.Errors));

        var lever = edit.Candidate!.LeverCatalog.All.Single(l => l.Key == "oss.improve");
        var snapshot = GoldenFixtures.Eai10969() with { OpenSourceScore = 75m };

        Assert.False(lever.IsAlreadyAchieved(snapshot));
        Assert.Equal(80m, lever.Apply(snapshot, null).OpenSourceScore);

        Assert.Contains(edit.Changes, c => c.Code == NoticeCodes.ChangeBands && c.Args["to"] == "0-59=20; 60-79=60; 80-100=100");
    }
}

/// <summary>El impacto que se ve antes de publicar sale del mismo motor que puntuara despues.</summary>
public sealed class RulesImpactTests
{
    private static readonly DateOnly Start = new(2026, 9, 12);

    private static List<ApplicationSnapshot> Portfolio() => [GoldenFixtures.Eai14381(), GoldenFixtures.Eai10969()];

    [Fact]
    public void TheSameRulesMoveNobody()
    {
        var impact = RulesImpactSimulator.Simulate(Portfolio(), RulesModel.BuiltIn, RulesModel.BuiltIn, Start);

        Assert.Equal(2, impact.Unchanged);
        Assert.Equal(0, impact.Up + impact.Down);
        Assert.Equal(0m, impact.AverageDelta);
        Assert.Empty(impact.Movers);
        Assert.Equal(impact.AlertsBefore, impact.AlertsAfter);
    }

    [Fact]
    public void TheImpactUsesTheEngineThatWillScore()
    {
        var candidate = RulesEditor.Apply(
            RulesModel.BuiltIn,
            new RulesDraft(Metrics: [new MetricDraft("kenna", 5), new MetricDraft("mfa", 10)])).Candidate!;

        var impact = RulesImpactSimulator.Simulate(Portfolio(), RulesModel.BuiltIn, candidate, Start);

        var mover = Assert.Single(impact.Movers, m => m.EaiCode == "10969");

        Assert.Equal(79.61m, mover.Before);
        Assert.Equal(82.01m, mover.After);
        Assert.Equal(2.40m, mover.Delta);
        Assert.Equal(2, impact.Up + impact.Down + impact.Unchanged);
    }

    /// <summary>Una aplicacion cuyo dato ya no cae en ninguna banda no desaparece del impacto: se cuenta.</summary>
    [Fact]
    public void AnApplicationWhoseValueLeavesEveryBandIsCountedNotHidden()
    {
        var narrow = RulesEditor.Apply(
            RulesModel.BuiltIn,
            new RulesDraft(Metrics: [new MetricDraft("oss", 10, [new(0, 10, 20, "0-10"), new(11, 20, 60, "11-20"), new(21, 30, 100, "21-30")])])).Candidate!;

        var impact = RulesImpactSimulator.Simulate(Portfolio(), RulesModel.BuiltIn, narrow, Start);

        Assert.True(impact.NoLongerScored >= 1);
        Assert.Contains(impact.Movers, m => m.EaiCode == "10969" && m.After is null && m.Before == 79.61m);
    }
}
