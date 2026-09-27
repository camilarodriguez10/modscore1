using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Modernization.ChannelApi;
using Modernization.Domain;
using Modernization.Rules;
using Modernization.SystemApi.Contracts;

namespace Modernization.Api.Tests;

/// <summary>
/// Los tipos de la SPA (<c>src/web/src/api/types.ts</c>) se mantienen a mano. Esta prueba
/// impide que se separen de lo que la capa Channel serializa: cada propiedad de un modelo
/// de vista tiene que existir en su interfaz TypeScript, y al reves.
/// </summary>
/// <remarks>
/// El plan preveia un cliente generado desde OpenAPI (secc. 6.1). No se hizo: el OpenAPI
/// solo se publica en Development y generar tipos agregaba una herramienta a la cadena de
/// suministro. Esta prueba da la misma garantia —sin drift de tipos— con lo que ya hay.
/// </remarks>
public sealed partial class ViewModelContractTests
{
    /// <summary>Tipos que la capa Channel entrega tal como los produce la System, con su nombre en la SPA.</summary>
    private static readonly Dictionary<Type, string> SharedContracts = new()
    {
        [typeof(Notice)] = "Notice",
        [typeof(RulesVersionResponse)] = "RulesVersionView",
        [typeof(RulesVersionDocumentResponse)] = "RulesVersionDocumentView",
        [typeof(RulesSimulationResponse)] = "RulesSimulationView",
        [typeof(RulesPublishResponse)] = "RulesPublishView",
        [typeof(RulesCompareResponse)] = "RulesCompareView",
        [typeof(RulesTimelineResponse)] = "RulesTimelineView",
        [typeof(RulesTimelineEntryResponse)] = "RulesTimelineEntry",
        [typeof(RulesImpactSummary)] = "RulesImpactSummary",
        [typeof(RulesImpact)] = "RulesImpact",
        [typeof(BandShift)] = "BandShift",
        [typeof(ApplicationShift)] = "ApplicationShift",
        [typeof(ApplicationComparison)] = "ApplicationComparison",
        [typeof(MetricComparison)] = "MetricComparison",
        [typeof(PillarComparison)] = "PillarComparison",
        [typeof(RulesDocument)] = "RulesDocument",
        [typeof(RulesPillar)] = "RulesPillar",
        [typeof(RulesMetric)] = "RulesMetric",
        [typeof(RulesLever)] = "RulesLever",
        [typeof(RulesRoadmap)] = "RulesRoadmap",
        [typeof(RulesRoadmapPath)] = "RulesRoadmapPath",
        [typeof(RulesMilestone)] = "RulesMilestone",
        [typeof(InsightsResponse)] = "InsightsView",
        [typeof(PortfolioHealthResponse)] = "PortfolioHealthView",
        [typeof(BandShareResponse)] = "BandShareView",
        [typeof(ModernizationCandidateResponse)] = "ModernizationCandidateView",
        [typeof(ComplexityClusterResponse)] = "ComplexityClusterView",
        [typeof(RiskConcentrationResponse)] = "RiskConcentrationView",
        [typeof(ObsolescenceDriverResponse)] = "ObsolescenceDriverView",
        [typeof(ResilienceGapsResponse)] = "ResilienceGapsView",
        [typeof(ResilienceGapApplicationResponse)] = "ResilienceGapApplicationView",
        [typeof(InsightsOptionsResponse)] = "InsightsOptionsView",
        [typeof(DashboardResponse)] = "DashboardView",
        [typeof(DashboardTrendPoint)] = "DashboardTrendPoint",
        [typeof(DashboardSource)] = "DashboardSource",
        [typeof(Insights.PortfolioDashboard)] = "PortfolioDashboard",
        [typeof(Insights.DashboardKpi)] = "DashboardKpi",
        [typeof(Insights.DashboardHeatmap)] = "DashboardHeatmap",
        [typeof(Insights.DashboardHeatmapCell)] = "DashboardHeatmapCell",
        [typeof(Insights.DashboardHealth)] = "DashboardHealth",
        [typeof(Insights.BandShare)] = "BandShareView",
        [typeof(Insights.DashboardPrioritization)] = "DashboardPrioritization",
        [typeof(Insights.DashboardMatrixPoint)] = "DashboardMatrixPoint",
        [typeof(Insights.DashboardComplexity)] = "DashboardComplexity",
        [typeof(Insights.DashboardComplexityDimension)] = "DashboardComplexityDimension",
        [typeof(Insights.DashboardComplexityOpportunity)] = "DashboardComplexityOpportunity",
        [typeof(Insights.DashboardRisk)] = "DashboardRisk",
        [typeof(Insights.DashboardHotspot)] = "DashboardHotspot",
        [typeof(Insights.DashboardObsolescence)] = "DashboardObsolescence",
        [typeof(Insights.DashboardObsolescenceFactor)] = "DashboardObsolescenceFactor",
        [typeof(Insights.DashboardResilience)] = "DashboardResilience",
        [typeof(Insights.DashboardResilienceDimension)] = "DashboardResilienceDimension",
        [typeof(Insights.DashboardLevelShare)] = "DashboardLevelShare",
        [typeof(Insights.DashboardApplication)] = "DashboardApplication",
        [typeof(Insights.AnalyticsDocument)] = "AnalyticsDocument",
        [typeof(Insights.AnalyticsParameterValue)] = "AnalyticsParameterValue",
        [typeof(Insights.AnalyticsAreaValue)] = "AnalyticsAreaValue",
        [typeof(IndicatorShift)] = "IndicatorShift",
        [typeof(RulesDraft)] = "RulesDraft",
        [typeof(AnalyticsDraft)] = "AnalyticsDraft",
        [typeof(AnalyticsAreaDraft)] = "AnalyticsAreaDraft",
        [typeof(CodeResponse)] = "CodeView",
        [typeof(CodeApplicationResponse)] = "CodeApplicationView",
        [typeof(CodeSourceResponse)] = "CodeSource",
        [typeof(Insights.CodePortfolio)] = "CodePortfolio",
        [typeof(Insights.CodeKpis)] = "CodeKpis",
        [typeof(Insights.CodeStatusShare)] = "CodeStatusShare",
        [typeof(Insights.CodeLifecycleRow)] = "CodeLifecycleRow",
        [typeof(Insights.CodeLibraryRow)] = "CodeLibraryRow",
        [typeof(Insights.CodeLevelShare)] = "CodeLevelShare",
        [typeof(Insights.CodeApplication)] = "CodeApplication",
        [typeof(Insights.CodeApplicationDetail)] = "CodeApplicationDetail",
        [typeof(Insights.CodeRiskFactor)] = "CodeRiskFactor",
        [typeof(Insights.CodeRepository)] = "CodeRepository",
        [typeof(Insights.CodeComponent)] = "CodeComponent",
        [typeof(Insights.CodeDependency)] = "CodeDependency",
    };

    [Fact]
    public void EveryViewModelMatchesItsTypeScriptInterface()
    {
        var interfaces = ReadInterfaces();
        var mismatches = new List<string>();

        var viewModels = typeof(ChannelApiMarker).Assembly
            .GetTypes()
            .Where(t => t is { IsPublic: true, IsClass: true } && t.Namespace == "Modernization.ChannelApi.ViewModels")
            .ToDictionary(t => t, t => t.Name);

        foreach (var (type, name) in viewModels.Concat(SharedContracts))
        {
            if (!interfaces.TryGetValue(name, out var tsProperties))
            {
                mismatches.Add($"{type.Name}: there is no 'export interface {name}' in types.ts");
                continue;
            }

            var csProperties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && p.GetCustomAttribute<JsonIgnoreAttribute>() is null)
                .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
                .ToHashSet(StringComparer.Ordinal);

            foreach (var missing in csProperties.Except(tsProperties).Order(StringComparer.Ordinal))
            {
                mismatches.Add($"{name}.{missing}: serialized by {type.Name} but missing in types.ts");
            }

            foreach (var extra in tsProperties.Except(csProperties).Order(StringComparer.Ordinal))
            {
                mismatches.Add($"{name}.{extra}: declared in types.ts but not serialized by {type.Name}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
    }

    private static Dictionary<string, HashSet<string>> ReadInterfaces()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "web", "src", "api", "types.ts"));
        var interfaces = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (Match match in InterfacePattern().Matches(text))
        {
            interfaces[match.Groups["name"].Value] = [.. PropertyPattern()
                .Matches(match.Groups["body"].Value)
                .Select(p => p.Groups["property"].Value)];
        }

        return interfaces;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ModernizationScore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    [GeneratedRegex(@"export interface (?<name>\w+)\s*\{(?<body>[^}]*)\}", RegexOptions.Singleline)]
    private static partial Regex InterfacePattern();

    [GeneratedRegex(@"^\s*(?:readonly\s+)?(?<property>\w+)\??\s*:", RegexOptions.Multiline)]
    private static partial Regex PropertyPattern();
}
