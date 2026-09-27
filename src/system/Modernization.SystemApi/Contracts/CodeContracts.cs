using Modernization.Insights;

namespace Modernization.SystemApi.Contracts;

/// <summary>
/// La pantalla Code sobre el portafolio filtrado, con de dónde salen los datos: el
/// inventario de repositorios —de demostración mientras no haya integración— y el
/// catálogo de fin de vida (ADR 0026).
/// </summary>
/// <remarks>
/// <c>Trend</c> es el portafolio filtrado en cada carga: score, Currency Score de CMT y
/// Risk Score promedio. La proyección a uno, dos y tres años viaja en el análisis.
/// </remarks>
public sealed record CodeResponse(
    string RulesetVersion,
    CodeSourceResponse Source,
    CodePortfolio Analysis,
    IReadOnlyList<DashboardTrendPoint> Trend,
    AnalyticsDocument Definitions);

/// <summary>Una aplicación con sus repositorios, componentes y dependencias, y su evolución por carga.</summary>
public sealed record CodeApplicationResponse(
    string RulesetVersion,
    DateOnly AsOf,
    CodeSourceResponse Source,
    CodeApplicationDetail Detail,
    IReadOnlyList<CodeHistoryPoint> History,
    AnalyticsDocument Definitions);

/// <summary>Una aplicación en una carga. Null donde no existía, estaba fuera del alcance o no tenía el dato.</summary>
public sealed record CodeHistoryPoint(
    string VersionId,
    string Label,
    DateTimeOffset CapturedAt,
    decimal? Score,
    decimal? Currency,
    decimal? RiskScore)
{
    public static CodeHistoryPoint From(Domain.Sources.ScoreHistoryPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        return new CodeHistoryPoint(point.VersionId, point.Label, point.CapturedAt, point.Score, point.Currency, point.RiskScore);
    }
}

/// <summary>
/// De dónde sale lo que se ve. <c>Connected</c> es false con datos de demostración y sin
/// integración: la pantalla lo dice con esas palabras, no lo esconde.
/// </summary>
public sealed record CodeSourceResponse(
    string Provider,
    bool Connected,
    DateTimeOffset? ScannedAt,
    string Catalog,
    string CatalogUrl,
    DateOnly CatalogRetrievedOn,
    int CatalogProducts,
    int CatalogCycles)
{
    public static CodeSourceResponse From(Domain.Code.CodeInventorySet inventory, EndOfLifeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(catalog);

        return new CodeSourceResponse(
            inventory.Provider,
            inventory.Connected,
            inventory.ScannedAt,
            catalog.Source,
            catalog.SourceUrl,
            catalog.RetrievedOn,
            catalog.Products.Count,
            catalog.CycleCount);
    }
}
