using Modernization.Domain;
using Modernization.Domain.Code;
using Modernization.Insights;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// La pantalla Code sobre un portafolio de prueba que se puede verificar a mano, con el
/// catálogo real de endoflife.date y las reglas de fábrica.
/// </summary>
/// <remarks>
/// Cuatro aplicaciones al 2026-09-26. La 100 es vigente según CMT (85) pero corre Node 14 y
/// jQuery 1: obsolescencia oculta. La 200 es legada (40) y ya corre .NET 10: el inventario
/// quedó atrás. La 300 es COBOL, sin repositorios. La 400 queda fuera del filtro, y su
/// repositorio no puede aparecer en ningún conteo.
/// </remarks>
public sealed class CodeAnalyzerTests
{
    private static readonly DateOnly AsOf = new(2026, 9, 26);
    private static readonly EndOfLifeCatalog Catalog = EndOfLifeCatalog.Default;
    private static readonly DashboardRules Rules = DashboardRules.Default;

    private static readonly ApplicationSnapshot Hidden = Application("100", 85m, critical: true);
    private static readonly ApplicationSnapshot Behind = Application("200", 40m);
    private static readonly ApplicationSnapshot Cobol = Application("300", 30m);
    private static readonly ApplicationSnapshot Outside = Application("400", 90m);

    private static readonly CodeInventorySet Inventory = new()
    {
        Provider = "Test",
        Connected = false,
        ScannedAt = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
        Repositories =
        [
            new RepositoryInventory
            {
                EaiCode = "100",
                Name = "org/hidden-web",
                DefaultBranch = "main",
                LastCommit = new DateOnly(2026, 9, 1),
                Components =
                [
                    new("nodejs", "14.21.3", ".nvmrc"),           // fin de vida 2023-04-30
                    new("jquery", "1.12.4", "package.json"),      // discontinuado
                    new("ubuntu", "22.04", "Dockerfile"),         // 2027-06-01: próximo
                ],
                Dependencies =
                [
                    Library("lodash", "4.17.11", "4.17.21", critical: 1, high: 2),
                    Library("request", "2.88.2", "2.88.2", deprecated: true),
                    Library("typescript", "2.9.2", "5.9.2"),       // tres mayores atrás: desactualizada
                    Library("node-sass", "4.14.1", "9.0.0", deprecated: true), // obsoleta: cuenta una vez
                ],
            },
            new RepositoryInventory
            {
                EaiCode = "100",
                Name = "org/hidden-batch",
                DefaultBranch = "master",
                LastCommit = new DateOnly(2025, 6, 1),            // más de 12 meses: abandonado
                Components = [new("python", "3.13.1", ".python-version")],
                Dependencies = [],
            },
            new RepositoryInventory
            {
                EaiCode = "200",
                Name = "org/behind-api",
                DefaultBranch = "main",
                LastCommit = new DateOnly(2026, 9, 20),
                Archived = true,
                Components = [new("dotnet", "10.0.0", "global.json"), new("websphere", "9.0.5.20", "server.xml")],
                Dependencies = [Library("Serilog", "4.2.0", "4.3.0", ecosystem: "NuGet")],
            },
            new RepositoryInventory
            {
                EaiCode = "400",
                Name = "org/outside-api",
                DefaultBranch = "main",
                LastCommit = new DateOnly(2026, 9, 20),
                Components = [new("nodejs", "10.24.1", ".nvmrc")],
                Dependencies = [Library("lodash", "4.17.11", "4.17.21", critical: 1, high: 2)],
            },
        ],
    };

    private static CodePortfolio Analyze(DashboardRules? rules = null)
        => CodeAnalyzer.Analyze([Hidden, Behind, Cobol], Inventory, Catalog, rules ?? Rules, AsOf);

    [Fact]
    public void OnlyTheRepositoriesOfTheApplicationsReceivedCount()
    {
        var kpis = Analyze().Kpis;

        Assert.Equal(3, kpis.Applications);
        Assert.Equal(2, kpis.ApplicationsWithRepositories);
        Assert.Equal(3, kpis.Repositories);
        Assert.Equal(6, kpis.Components);
        Assert.DoesNotContain(Analyze().Lifecycle, row => row.Product == "nodejs" && row.Cycle == "10");
    }

    [Fact]
    public void EachComponentGetsItsLifecycleStatus()
    {
        var portfolio = Analyze();
        var status = portfolio.Statuses.ToDictionary(s => s.Status, s => s.Components);

        Assert.Equal(2, status[EndOfLifeStatus.EndOfLife]);     // Node 14, jQuery 1
        Assert.Equal(1, status[EndOfLifeStatus.Approaching]);   // Ubuntu 22.04
        Assert.Equal(2, status[EndOfLifeStatus.Supported]);     // Python 3.13, .NET 10
        Assert.Equal(1, status[EndOfLifeStatus.Unknown]);       // WebSphere: fuera del catálogo

        var node = portfolio.Lifecycle.Single(r => r.Product == "nodejs");
        Assert.Equal("14", node.Cycle);
        Assert.Equal(new DateOnly(2023, 4, 30), node.Eol);
        Assert.Equal(-40, node.MonthsToEol);

        var jquery = portfolio.Lifecycle.Single(r => r.Product == "jquery");
        Assert.True(jquery.Discontinued);

        // Primero lo vencido, por fecha; lo que no se puede evaluar, al final.
        Assert.Equal(EndOfLifeStatus.EndOfLife, portfolio.Lifecycle[0].Status);
        Assert.Equal(EndOfLifeStatus.Unknown, portfolio.Lifecycle[^1].Status);
    }

    [Fact]
    public void TheRiskIndexAddsThePointsOfEveryFinding()
    {
        var detail = CodeAnalyzer.Detail(Hidden, Inventory, Catalog, Rules, AsOf);
        var factors = detail.Factors.ToDictionary(f => f.Key);

        Assert.Equal(2, factors[CodeFactors.EolComponent].Count);
        Assert.Equal(1, factors[CodeFactors.ApproachingComponent].Count);
        Assert.Equal(1, factors[CodeFactors.CriticalVulnerability].Count);
        Assert.Equal(2, factors[CodeFactors.HighVulnerability].Count);
        Assert.Equal(2, factors[CodeFactors.DeprecatedLibrary].Count);
        Assert.Equal(1, factors[CodeFactors.OutdatedLibrary].Count);  // typescript; node-sass ya cuenta como obsoleta
        Assert.Equal(1, factors[CodeFactors.StaleRepository].Count);

        // 2×10 + 1×4 + 1×5 + 2×2 + 2×3 + 1×1 + 1×5 = 45: alto (25) pero no crítico (50).
        Assert.Equal(45m, detail.Application.RiskIndex);
        Assert.Equal(detail.Factors.Sum(f => f.Total), detail.Application.RiskIndex);
        Assert.Equal("high", detail.Application.RiskLevel);
        Assert.Equal(EndOfLifeStatus.EndOfLife, detail.Application.WorstStatus);
    }

    [Fact]
    public void CodeAndInventoryDisagreementsAreNamed()
    {
        var applications = Analyze().Applications.ToDictionary(a => a.EaiCode);

        Assert.Equal(CodeMismatch.HiddenObsolescence, applications["100"].Mismatch);
        Assert.Equal(CodeMismatch.InventoryBehind, applications["200"].Mismatch);
        Assert.Null(applications["300"].Mismatch);
        Assert.Equal(2, Analyze().Kpis.MismatchApplications);
    }

    [Fact]
    public void AnApplicationWithoutRepositoriesHasNoLevel()
    {
        var portfolio = Analyze();
        var cobol = portfolio.Applications.Single(a => a.EaiCode == "300");

        Assert.Null(cobol.RiskIndex);
        Assert.Null(cobol.RiskLevel);
        Assert.Equal(1, portfolio.RiskLevels.Single(l => l.Level == "none").Applications);

        // Sin nivel va al final de la lista, no al principio.
        Assert.Equal("300", portfolio.Applications[^1].EaiCode);
    }

    [Fact]
    public void AnArchivedRepositoryIsStaleEvenWithRecentCommits()
    {
        var behind = CodeAnalyzer.Detail(Behind, Inventory, Catalog, Rules, AsOf);

        Assert.True(behind.Repositories.Single().Stale);
        Assert.Equal(1, behind.Application.StaleRepositories);
        Assert.Equal(1, behind.Application.UnknownComponents);
    }

    [Fact]
    public void LibrariesWithFindingsAreRankedByTheirAlerts()
    {
        var libraries = Analyze().Libraries;

        Assert.Equal("lodash", libraries[0].Name);
        Assert.Equal(1, libraries[0].Applications);             // la 400 no cuenta
        Assert.Contains(libraries, l => l.Name == "request" && l.Deprecated);
        Assert.Contains(libraries, l => l.Name == "typescript" && l.Outdated && l.MajorsBehind == 3);
        Assert.DoesNotContain(libraries, l => l.Name == "Serilog");
    }

    [Fact]
    public void TheParametersChangeTheResult()
    {
        // Con una ventana de 6 meses, Ubuntu 22.04 (junio de 2027) deja de estar próximo.
        var narrow = Rules with { Code = Rules.Code with { ApproachingMonths = 6 } };
        Assert.Equal(0, Analyze(narrow).Kpis.ApproachingComponents);

        // Con los niveles más bajos, la 100 pasa a crítica.
        var strict = Rules with { Code = Rules.Code with { ModerateFrom = 5m, HighFrom = 10m, CriticalFrom = 40m } };
        Assert.Equal("critical", Analyze(strict).Applications.Single(a => a.EaiCode == "100").RiskLevel);

        // Con cinco mayores de tolerancia, TypeScript 2 ya no está desactualizada.
        var tolerant = Rules with { Code = Rules.Code with { OutdatedMajorsBehind = 5 } };
        Assert.DoesNotContain(Analyze(tolerant).Libraries, l => l.Name == "typescript");
    }

    [Theory]
    [InlineData("5.2.10.Final", "7.0.5.Final", 2)]
    [InlineData("33.3.1-jre", "33.4.8-jre", 0)]
    [InlineData("0.19.0", "1.12.2", 1)]
    [InlineData("v0.0.0-20200622213623-75b288015ac9", "v0.42.0", 0)]
    [InlineData("3.0", "2.0", 0)]
    public void MajorsBehindComparesTheFirstNumber(string version, string latest, int expected)
        => Assert.Equal(expected, CodeAnalyzer.MajorsBehind(version, latest));

    [Fact]
    public void WithoutALatestVersionThereIsNoGap()
    {
        Assert.Null(CodeAnalyzer.MajorsBehind("1.0", null));
        Assert.Null(CodeAnalyzer.MajorsBehind("release", "2.0"));
    }

    [Fact]
    public void WithoutAnInventoryEverythingIsEmptyButTheApplications()
    {
        var portfolio = CodeAnalyzer.Analyze([Hidden, Behind], CodeInventorySet.Empty("None"), Catalog, Rules, AsOf);

        Assert.Equal(2, portfolio.Kpis.Applications);
        Assert.Equal(0, portfolio.Kpis.Repositories);
        Assert.Empty(portfolio.Lifecycle);
        Assert.Empty(portfolio.Libraries);
        Assert.All(portfolio.Applications, a => Assert.Null(a.RiskLevel));
    }

    // -------------------------------------------------------------- en el tiempo

    [Fact]
    public void TheOutlookStartsTodayAndOnlyGetsWorseIfNothingChanges()
    {
        var outlook = Analyze().Outlook;

        Assert.Equal([0, 1, 2, 3], outlook.Select(h => h.Years));
        Assert.Equal(AsOf.AddYears(3), outlook[^1].On);
        Assert.Equal(Analyze().Kpis.EolComponents, outlook[0].EolComponents);

        for (var i = 1; i < outlook.Count; i++)
        {
            Assert.True(outlook[i].EolComponents >= outlook[i - 1].EolComponents);
            Assert.True(outlook[i].Currency <= outlook[i - 1].Currency);
        }
    }

    /// <summary>
    /// La 100 hoy tiene Node 14 y jQuery 1 vencidos; Ubuntu 22.04 vence en junio de 2027 y
    /// Python 3.13 en octubre de 2029. En un año son tres vencidos de cuatro evaluables.
    /// </summary>
    [Fact]
    public void AnApplicationIsProjectedComponentByComponent()
    {
        var outlook = CodeAnalyzer.Detail(Hidden, Inventory, Catalog, Rules, AsOf).Outlook;

        Assert.Equal([2, 3, 3, 3], outlook.Select(h => h.EolComponents));
        Assert.Equal(50m, outlook[0].Currency);   // 2 de 4 con soporte
        Assert.Equal(25m, outlook[1].Currency);   // 1 de 4
        Assert.Equal(outlook[0].RiskIndex, CodeAnalyzer.Detail(Hidden, Inventory, Catalog, Rules, AsOf).Application.RiskIndex);

        // Hoy: 2 vencidos + 1 próximo. En un año: 3 vencidos y el repositorio web también
        // abandonado si nadie hace commits (último: septiembre de 2026, umbral 12 meses).
        Assert.True(outlook[1].RiskIndex > outlook[0].RiskIndex);
        Assert.Equal(3, Analyze().Applications.Single(a => a.EaiCode == "100").ProjectedEolComponents);
    }

    [Fact]
    public void TheSuggestionsSayWhatToChangeAndWhen()
    {
        var suggestions = CodeAnalyzer.Detail(Hidden, Inventory, Catalog, Rules, AsOf).Suggestions;

        var node = suggestions.Single(s => s.Subject == "Node.js");
        Assert.Equal(CodeActions.Upgrade, node.Kind);
        Assert.Equal(CodeUrgency.Now, node.Urgency);
        Assert.Equal("14", node.Current);
        Assert.Equal("26", node.Target);                  // la LTS que más dura
        Assert.Equal(new DateOnly(2029, 4, 30), node.TargetEol);
        Assert.Equal(10m, node.PointsNow);

        var ubuntu = suggestions.Single(s => s.Subject == "Ubuntu");
        Assert.Equal(CodeUrgency.Year1, ubuntu.Urgency);
        Assert.Equal("26.04", ubuntu.Target);
        Assert.Equal(4m, ubuntu.PointsNow);
        Assert.Equal(6m, ubuntu.PointsAvoided);           // de 4 hoy a 10 al vencer

        Assert.Contains(suggestions, s => s.Kind == CodeActions.ReplaceLibrary && s.Subject == "request");
        Assert.Contains(suggestions, s => s.Kind == CodeActions.UpdateLibrary && s.Subject == "lodash" && s.Target == "4.17.21");
        Assert.Contains(suggestions, s => s.Kind == CodeActions.MaintainRepository && s.Repository == "org/hidden-batch");
        Assert.DoesNotContain(suggestions, s => s.Subject == "Python");   // vence en 2029: fuera de los tres años

        // Lo urgente primero.
        Assert.Equal(CodeUrgency.Now, suggestions[0].Urgency);
        Assert.Equal(CodeUrgency.Planned, suggestions[^1].Urgency);
    }

    [Theory]
    [InlineData("dotnet", "8.0.11", CodeActions.Upgrade, "10")]
    [InlineData("oracle-jdk", "1.8.0_392", CodeActions.Upgrade, "25")]
    [InlineData("angularjs", "1.8.3", CodeActions.Migrate, "22")]
    [InlineData("spring-boot", "4.1.0", CodeActions.AwaitRelease, null)]
    [InlineData("log4j", "1.2.17", CodeActions.Upgrade, "2")]
    [InlineData("mssqlserver", "13.0-sp3", CodeActions.Upgrade, "2025")]
    public void EachComponentGoesToTheRightTarget(string product, string version, string kind, string? target)
    {
        var application = Application("900", 70m);
        var inventory = new CodeInventorySet
        {
            Provider = "Test",
            Connected = false,
            Repositories =
            [
                new RepositoryInventory
                {
                    EaiCode = "900", Name = "org/app", DefaultBranch = "main", LastCommit = AsOf,
                    Components = [new(product, version, "test")], Dependencies = [],
                },
            ],
        };

        var suggestion = Assert.Single(CodeAnalyzer.Detail(application, inventory, Catalog, Rules, AsOf).Suggestions);

        Assert.Equal(kind, suggestion.Kind);
        Assert.Equal(target, suggestion.Target);
    }

    [Fact]
    public void ThePlanGroupsTheSameActionAcrossRepositories()
    {
        var plan = Analyze().Actions;

        Assert.All(plan, row => Assert.NotEqual(CodeActions.MaintainRepository, row.Kind));
        var node = plan.Single(r => r.Subject == "Node.js");
        Assert.Equal(1, node.Applications);
        Assert.True(plan.Count <= Rules.Code.TopActions);
        Assert.Equal(CodeUrgency.Now, plan[0].Urgency);
    }

    private static ApplicationSnapshot Application(string eai, decimal currency, bool critical = false) => new()
    {
        EaiCode = eai,
        ApplicationName = "App " + eai,
        CapturedAt = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero),
        RawTechScore = currency,
        CriticalApplicationIndicator = critical,
        Lob = "Individual",
        Country = "Chile",
    };

    private static RepositoryDependency Library(
        string name,
        string version,
        string latest,
        bool deprecated = false,
        int critical = 0,
        int high = 0,
        string ecosystem = "npm") => new()
    {
        Ecosystem = ecosystem,
        Name = name,
        Version = version,
        Latest = latest,
        Deprecated = deprecated,
        Critical = critical,
        High = high,
    };
}
