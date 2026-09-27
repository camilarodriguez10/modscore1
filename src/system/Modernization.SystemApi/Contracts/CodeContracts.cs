using Modernization.Insights;

namespace Modernization.SystemApi.Contracts;

/// <summary>
/// La pantalla Code sobre el portafolio filtrado, con de dónde salen los datos: el
/// inventario de repositorios —de demostración mientras no haya integración— y el
/// catálogo de fin de vida (ADR 0026).
/// </summary>
public sealed record CodeResponse(
    string RulesetVersion,
    CodeSourceResponse Source,
    CodePortfolio Analysis,
    AnalyticsDocument Definitions);

/// <summary>Una aplicación con sus repositorios, componentes y dependencias.</summary>
public sealed record CodeApplicationResponse(
    string RulesetVersion,
    DateOnly AsOf,
    CodeSourceResponse Source,
    CodeApplicationDetail Detail,
    AnalyticsDocument Definitions);

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
