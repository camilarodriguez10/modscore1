using Modernization.Domain;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Snapshots que reproducen las dos aplicaciones documentadas en las fuentes
/// originales. Son la red de seguridad del motor: si el score de alguna cambia,
/// el modelo dejo de coincidir con el dashboard y hay que investigar antes de
/// tocar nada mas.
/// </summary>
internal static class GoldenFixtures
{
    internal static readonly DateTimeOffset CapturedAt = new(2026, 9, 8, 21, 24, 0, TimeSpan.Zero);

    /// <summary>
    /// EAI 14381 — ejemplo trabajado del BRD ("Overall Modernization Score
    /// Calculation"). Todos los valores se derivan de datos crudos, por lo que
    /// este fixture ejercita el pipeline completo: extraccion, mapeo y ponderacion.
    ///
    /// Esperado segun el BRD:
    ///   Risk         17,00 / 25 -> 74,40
    ///   Architecture 21,75 / 25 -> 89,60
    ///   Stability     6,00 / 10 -> 68,00
    ///   Global       44,75 / 60 -> 79,67
    /// </summary>
    internal static ApplicationSnapshot Eai14381() => new()
    {
        EaiCode = "14381",
        ApplicationName = "BRD Worked Example",
        CapturedAt = CapturedAt,

        // Risk — numerador 8,00 + 7,00 + 2,00 = 17,00 sobre denominador 25.
        KennaScore = 120,                    // 0-339 (Low)      -> 100 x 8%  = 8,00
        DisasterRecoveryInScope = null,      // No Data Available -> excluida (7)
        ScaFlaws = null,                     // No Data Available -> excluidas (10 + 8)
        UtilizesMfa = true,                  // Yes (with users)  -> 100 x 7%  = 7,00
        UserCount = 350,
        OpenSourceScore = 28.95m,            // 0-59              -> 20  x 10% = 2,00

        // Architecture — numerador 5,00 + 9,75 + 7,00 = 21,75 sobre denominador 25.
        RawTechScore = 97.67m,               // 75-100            -> 100 x 5%  = 5,00
        NativeApi = null,                    // No Data Available -> excluida (5)
        ArchitecturalValue = new ArchitecturalValue
        {
            ModernCount = 15,
            HeritageCount = 1,
            ObsoleteCount = 0,
            NotApprovedForUseCount = 0,      // (100x15 + 60x1)/16 = 97,50 x 10% = 9,75
        },
        ProductStrategy = new ProductStrategy
        {
            InvestCount = 2,
            TolerateCount = 1,
            EliminateCount = 1,
            MigrateCount = 0,                // (100x2 + 60 + 20)/4 = 70,00 x 10% = 7,00
        },
        TechDebtScore = null,                // No Data Available -> excluida (10)

        // Stability — numerador 1,00 + 5,00 = 6,00 sobre denominador 10.
        Monitoring = new MonitoringCapabilities
        {
            ElasticAgent = false,
            ApplicationPerformanceMonitoring = false,
            SyntheticMonitoring = false,
            GeneralMonitoring = false,       // None              -> 20  x 5%  = 1,00
        },
        OperationalScore = 92m,              // 80-100            -> 100 x 5%  = 5,00
    };

    /// <summary>
    /// EAI 10969 — captura del dashboard Power BI v1.3.
    ///
    /// Los compuestos vienen ya calculados desde CMT y MTS (75,72 y 88,37). Son
    /// valores que ningun conteo pequeno de productos puede producir: exigirian
    /// del orden de mil productos, asi que el dashboard los publica agregados.
    /// Por eso este fixture usa ProvidedValue en lugar de conteos.
    ///
    /// Esperado segun el dashboard:
    ///   Risk         23,20 / 40 -> 66,40
    ///   Architecture 26,41 / 30 -> 90,42
    ///   Stability    10,00 / 10 -> 100,00
    ///   Global       59,61 / 80 -> 79,61
    /// </summary>
    internal static ApplicationSnapshot Eai10969() => new()
    {
        EaiCode = "10969",
        ApplicationName = "Dashboard v1.3 Reference",
        CapturedAt = CapturedAt,

        // Risk — numerador 1,60 + 7,00 + 1,60 + 7,00 + 6,00 = 23,20 sobre 40.
        KennaScore = 750,                    // 670-1000          -> 20  x 8%  = 1,60
        DisasterRecoveryInScope = true,      // Yes               -> 100 x 7%  = 7,00
        ScaFlaws = new ScaFlawCounts
        {
            Critical = null,                 // No Data Available -> excluida (10)
            High = 3,                        // Present           -> 20  x 8%  = 1,60
            Medium = 12,
            Low = 4,
        },
        UtilizesMfa = true,                  // Yes (with users)  -> 100 x 7%  = 7,00
        UserCount = 1200,
        OpenSourceScore = 68m,               // 60-74             -> 60  x 10% = 6,00

        // Architecture — numerador 5,00 + 5,00 + 7,572 + 8,837 = 26,409 sobre 30.
        RawTechScore = 97m,                  // 75-100            -> 100 x 5%  = 5,00
        NativeApi = ApiExposure.Apim,        // APIM              -> 100 x 5%  = 5,00
        ArchitecturalValue = new ArchitecturalValue { ProvidedValue = 75.72m },  // x 10% = 7,572
        ProductStrategy = new ProductStrategy { ProvidedValue = 88.37m },        // x 10% = 8,837
        TechDebtScore = null,                // No Data Available -> excluida (10)

        // Stability — numerador 5,00 + 5,00 = 10,00 sobre 10.
        Monitoring = new MonitoringCapabilities
        {
            ElasticAgent = true,             // Enabled           -> 100 x 5%  = 5,00
            ApplicationPerformanceMonitoring = true,
            SyntheticMonitoring = false,
            GeneralMonitoring = true,
        },
        OperationalScore = 95m,              // 80-100            -> 100 x 5%  = 5,00
    };
}
