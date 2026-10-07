using Modernization.Domain;

namespace Modernization.Scoring.Engine.Tests;

/// <summary>
/// Tabla de casos de las reglas de mapeo del BRD, incluidos los limites exactos
/// de cada banda. Los bordes son donde un modelo de scoring se rompe en silencio.
/// </summary>
public sealed class MappingRuleTests
{
    private readonly ScoringEngine _engine = new(RulesetCatalog.Default);

    private static ApplicationSnapshot Empty() => new()
    {
        EaiCode = "TEST",
        ApplicationName = "Fixture",
        CapturedAt = GoldenFixtures.CapturedAt,
    };

    private decimal? ScoreOf(ApplicationSnapshot snapshot, string metricKey)
        => _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == metricKey).MappedScore;

    // ------------------------------------------------------------------ Kenna

    [Theory]
    [InlineData(1, 100)]     // el 0 se excluye (ADR 0019), ver ZeroIsExcluded...
    [InlineData(339, 100)]   // limite superior de Low
    [InlineData(340, 60)]    // limite inferior de Medium
    [InlineData(669, 60)]
    [InlineData(670, 20)]    // limite inferior de Critical/High
    [InlineData(1000, 20)]
    public void KennaScore_MapsByTier(int kenna, decimal expected)
        => Assert.Equal(expected, ScoreOf(Empty() with { KennaScore = kenna }, "kenna"));

    // ------------------------------------------------------- OSS y RAW Tech

    /// <summary>
    /// Desde el 2026-10-07 (ADR 0032), OSS y Raw Tech entran con su valor bruto, como el
    /// reporte v1.3: 37 vale 37, no la banda 20. Fuera de 0-100 se acota.
    /// </summary>
    [Theory]
    [InlineData(1, 1)]       // el 0 se excluye (ADR 0019)
    [InlineData(37, 37)]
    [InlineData(59.5, 59.5)]
    [InlineData(74, 74)]
    [InlineData(100, 100)]
    [InlineData(130, 100)]
    public void OpenSourceScore_IsTheRawValue(double oss, double expected)
        => Assert.Equal((decimal)expected, ScoreOf(Empty() with { OpenSourceScore = (decimal)oss }, "oss"));

    [Theory]
    [InlineData(59, 59)]
    [InlineData(88.48, 88.48)]
    [InlineData(100, 100)]
    public void RawTechScore_IsTheRawValue(double rawTech, double expected)
        => Assert.Equal((decimal)expected, ScoreOf(Empty() with { RawTechScore = (decimal)rawTech }, "rawTech"));

    [Fact]
    public void AZeroOssOrRawTechIsStillExcludedLikeAMissingValue()
    {
        var result = _engine.Calculate(Empty() with { OpenSourceScore = 0m, RawTechScore = 0m });

        Assert.All(
            result.Metrics.Where(m => m.MetricKey is "oss" or "rawTech"),
            metric => Assert.Equal(NoticeCodes.ExclusionZeroValue, metric.Exclusion?.Code));
    }

    // -------------------------------------------------------------- Tech Debt

    [Theory]
    [InlineData(0, 100)]     // sin deuda es el mejor caso
    [InlineData(0.1, 60)]
    [InlineData(1.75, 60)]   // valor real del archivo tech_debt
    [InlineData(2.4, 60)]
    [InlineData(2.5, 20)]
    [InlineData(3.25, 20)]   // valor real del archivo tech_debt
    [InlineData(5, 20)]
    public void TechDebtScore_MapsInverted(double techDebt, decimal expected)
        => Assert.Equal(expected, ScoreOf(Empty() with { TechDebtScore = (decimal)techDebt }, "techDebt"));

    // ------------------------------------------------------------ Operational

    [Theory]
    [InlineData(0.5, 20)]    // el 0 se excluye (ADR 0019)
    [InlineData(59.9, 20)]
    [InlineData(60, 60)]
    [InlineData(79.99, 60)]
    [InlineData(80, 100)]
    [InlineData(100, 100)]
    public void OperationalScore_MapsByBand(double operational, decimal expected)
        => Assert.Equal(expected, ScoreOf(Empty() with { OperationalScore = (decimal)operational }, "operational"));

    // -------------------------------------------------------------------- MFA

    [Theory]
    [InlineData(true, 500, 100)]    // Yes (with users)
    [InlineData(true, 0, 100)]      // Yes (with no users)
    [InlineData(false, 500, 20)]    // No (with users) — el unico caso penalizado
    [InlineData(false, 0, 100)]     // No (with no users)
    public void Mfa_PenalizesOnlyWhenUsersExistWithoutMfa(bool utilizesMfa, int userCount, decimal expected)
        => Assert.Equal(expected, ScoreOf(Empty() with { UtilizesMfa = utilizesMfa, UserCount = userCount }, "mfa"));

    [Fact]
    public void Mfa_AssumesUsersWhenUserCountIsUnknown()
    {
        // Supuesto conservador: sin saber si hay usuarios, no tener MFA penaliza.
        var snapshot = Empty() with { UtilizesMfa = false, UserCount = null };

        Assert.Equal(20m, ScoreOf(snapshot, "mfa"));
    }

    // -------------------------------------------------------------- SCA flaws

    [Theory]
    [InlineData(0, 100)]     // "None" — cero flaws es el mejor caso
    [InlineData(1, 20)]
    [InlineData(47, 20)]
    public void ScaCritical_MapsByPresence(int count, decimal expected)
        => Assert.Equal(expected, ScoreOf(Empty() with { ScaFlaws = new ScaFlawCounts { Critical = count } }, "sca.critical"));

    [Fact]
    public void ScaLow_UsesItsOwnBandEvenThoughItDoesNotAffectTheScore()
    {
        var snapshot = Empty() with { ScaFlaws = new ScaFlawCounts { Low = 5 } };
        var metric = _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == "sca.low");

        Assert.Equal(60m, metric.MappedScore);
        Assert.True(metric.IsInformational);
        Assert.Equal(0m, metric.WeightedScore);
    }

    [Fact]
    public void ScaSeverity_WithoutDataIsExcluded_NotTreatedAsZero()
    {
        // La distincion es critica: "sin dato" excluye, "cero flaws" puntua 100.
        var snapshot = Empty() with { ScaFlaws = new ScaFlawCounts { Critical = null, High = 0 } };
        var result = _engine.Calculate(snapshot);

        Assert.False(result.Metrics.Single(m => m.MetricKey == "sca.critical").IsIncluded);
        Assert.Equal(100m, result.Metrics.Single(m => m.MetricKey == "sca.high").MappedScore);
    }

    // ------------------------------------------------------------------- APIs

    [Theory]
    [InlineData(ApiExposure.Apim, 100)]
    [InlineData(ApiExposure.Apic, 100)]
    [InlineData(ApiExposure.Soap, 20)]
    [InlineData(ApiExposure.DataPowerSoapLibraries, 20)]
    public void NativeApi_MapsByExposureType(ApiExposure exposure, decimal expected)
        => Assert.Equal(expected, ScoreOf(Empty() with { NativeApi = exposure }, "api.native"));

    // ------------------------------------------------------------- Monitoring

    [Theory]
    [InlineData(true, false, false, 100)]   // solo Elastic Agent
    [InlineData(false, true, false, 100)]   // solo APM
    [InlineData(false, false, true, 100)]   // solo Synthetics
    [InlineData(false, false, false, 20)]   // ninguna
    public void Monitoring_AnyCapabilityIsEnough(bool agent, bool apm, bool synthetics, decimal expected)
    {
        var snapshot = Empty() with
        {
            Monitoring = new MonitoringCapabilities
            {
                ElasticAgent = agent,
                ApplicationPerformanceMonitoring = apm,
                SyntheticMonitoring = synthetics,
                GeneralMonitoring = false,
            },
        };

        Assert.Equal(expected, ScoreOf(snapshot, "monitoring"));
    }

    [Fact]
    public void Monitoring_WithNoCapabilityReportedIsExcluded()
    {
        var snapshot = Empty() with { Monitoring = new MonitoringCapabilities() };
        var metric = _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == "monitoring");

        Assert.False(metric.IsIncluded);
    }

    // -------------------------------------------------------------- Roadmap

    [Theory]
    [InlineData(RoadmapType.MaintainEvolve, 100)]
    [InlineData(RoadmapType.MaintainPatch, 60)]
    [InlineData(RoadmapType.Rehost, 60)]
    [InlineData(RoadmapType.Refactor, 20)]
    [InlineData(RoadmapType.Reimagine, 20)]
    [InlineData(RoadmapType.Retire, 20)]
    [InlineData(RoadmapType.Consolidate, 20)]
    public void Roadmap_IsMappedButDoesNotAffectTheScore(RoadmapType roadmap, decimal expected)
    {
        var snapshot = Empty() with { Roadmap = roadmap };
        var metric = _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == "roadmap");

        Assert.Equal(expected, metric.MappedScore);
        Assert.True(metric.IsInformational);
        Assert.Equal(0m, metric.WeightedScore);
    }

    // ---------------------------------------------------------- Containerized

    [Fact]
    public void Containerized_NoDoesNotScore()
    {
        // BRD: "No" produce NULL, no un puntaje bajo.
        var metric = _engine.Calculate(Empty() with { Containerized = false })
            .Metrics.Single(m => m.MetricKey == "containerized");

        Assert.False(metric.IsIncluded);
    }

    // ------------------------------------------------------------------- DR

    /// <summary>
    /// La regla de DR del dashboard v1.3, confirmada el 2026-10-07: con el Critical
    /// Application Indicator en "N" la métrica queda sin dato; si no, Yes = 100 y No = 20.
    /// Un indicador desconocido no excluye.
    /// </summary>
    [Theory]
    [InlineData(true, true, 100)]
    [InlineData(true, false, 20)]
    [InlineData(null, true, 100)]
    [InlineData(null, false, 20)]
    public void DisasterRecovery_ScoresUnlessTheApplicationIsNotCritical(bool? critical, bool inScope, int expected)
        => Assert.Equal(
            expected,
            ScoreOf(Empty() with { CriticalApplicationIndicator = critical, DisasterRecoveryInScope = inScope }, "dr.inScope"));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DisasterRecovery_IsLeftOutOfANonCriticalApplication(bool inScope)
    {
        var snapshot = Empty() with { CriticalApplicationIndicator = false, DisasterRecoveryInScope = inScope };
        var metric = _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == "dr.inScope");

        Assert.False(metric.IsIncluded);
        Assert.Null(metric.MappedScore);
        Assert.Equal(NoticeCodes.ExclusionNotCritical, metric.Exclusion?.Code);
    }

    /// <summary>
    /// Solo DR lleva la regla: las demás métricas de una aplicación no crítica se puntúan
    /// igual que antes.
    /// </summary>
    [Fact]
    public void OnlyDisasterRecoveryDependsOnCriticality()
    {
        Assert.Equal(["dr.inScope"], RulesetCatalog.Default.Metrics.Where(m => m.CriticalOnly).Select(m => m.Key));
        Assert.Equal(100m, ScoreOf(Empty() with { CriticalApplicationIndicator = false, KennaScore = 120 }, "kenna"));
    }

    // ------------------------------------------------------------ Compuestos

    [Fact]
    public void ArchitecturalValue_DerivesWeightedAverageFromCounts()
    {
        var snapshot = Empty() with
        {
            ArchitecturalValue = new ArchitecturalValue
            {
                ModernCount = 15,
                HeritageCount = 1,
            },
        };

        // (100x15 + 60x1) / 16 = 97,50
        Assert.Equal(97.5m, ScoreOf(snapshot, "archValue"));
    }

    /// <summary>
    /// El ejemplo con que el dueño del reporte explicó el cálculo el 2026-10-07: 408
    /// componentes Modern, 415 Heritage y 105 Not Approved for Use, ninguno Obsolete.
    /// (40 800 + 24 900 + 2 100) / 928 = 73,06.
    /// </summary>
    [Fact]
    public void ArchitecturalValue_MatchesTheReportOwnersWorkedExample()
    {
        var snapshot = Empty() with
        {
            ArchitecturalValue = new ArchitecturalValue
            {
                ModernCount = 408,
                HeritageCount = 415,
                NotApprovedForUseCount = 105,
                ObsoleteCount = 0,
            },
        };

        // El motor guarda la precisión completa (73,0603…); el reporte muestra dos decimales.
        Assert.Equal(73.06m, Math.Round(ScoreOf(snapshot, "archValue")!.Value, 2));
    }

    [Fact]
    public void ArchitecturalValue_ProvidedValueWinsOverCounts()
    {
        var snapshot = Empty() with
        {
            ArchitecturalValue = new ArchitecturalValue
            {
                ProvidedValue = 75.72m,
                ModernCount = 15,
                HeritageCount = 1,
            },
        };

        Assert.Equal(75.72m, ScoreOf(snapshot, "archValue"));
    }

    [Fact]
    public void ArchitecturalValue_WithoutProductsIsExcluded()
    {
        var snapshot = Empty() with { ArchitecturalValue = new ArchitecturalValue() };
        var metric = _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == "archValue");

        Assert.False(metric.IsIncluded);
    }

    [Fact]
    public void ProductStrategy_DerivesWeightedAverageFromCounts()
    {
        var snapshot = Empty() with
        {
            ProductStrategy = new ProductStrategy
            {
                InvestCount = 2,
                TolerateCount = 1,
                EliminateCount = 1,
            },
        };

        // (100x2 + 60x1 + 20x1) / 4 = 70,00
        Assert.Equal(70m, ScoreOf(snapshot, "productStrategy"));
    }

    // ------------------------------------------ Cero frente a sin dato (ADR 0019)

    /// <summary>
    /// BRD, "ALL Sections": "Null or zero values are excluded from scoring". Donde la
    /// tabla del BRD no puntua el 0 como dato propio, un 0 sale del calculo como un nulo
    /// y lo dice con su propio motivo.
    /// </summary>
    [Theory]
    [InlineData("kenna")]
    [InlineData("oss")]
    [InlineData("rawTech")]
    [InlineData("operational")]
    public void ZeroIsExcludedLikeNull_WhereTheBrdTableDoesNotScoreIt(string metricKey)
    {
        var snapshot = Empty() with
        {
            KennaScore = 0,
            OpenSourceScore = 0m,
            RawTechScore = 0m,
            OperationalScore = 0m,
        };

        var metric = _engine.Calculate(snapshot).Metrics.Single(m => m.MetricKey == metricKey);

        Assert.Null(metric.MappedScore);
        Assert.Equal(NoticeCodes.ExclusionZeroValue, metric.Exclusion?.Code);
    }

    /// <summary>
    /// La regla especifica gana sobre la general: Tech Debt 0 -> 100 y SCA "None" -> 100
    /// estan escritos en sus tablas, asi que el 0 si puntua.
    /// </summary>
    [Fact]
    public void ZeroStillScores_WhereTheBrdTableNamesIt()
    {
        var snapshot = Empty() with
        {
            TechDebtScore = 0m,
            ScaFlaws = new ScaFlawCounts { Critical = 0, High = 0, Medium = 0, Low = 0 },
        };

        Assert.Equal(100m, ScoreOf(snapshot, "techDebt"));
        Assert.Equal(100m, ScoreOf(snapshot, "sca.critical"));
        Assert.Equal(100m, ScoreOf(snapshot, "sca.high"));
    }

    [Fact]
    public void ZeroExclusion_ReducesTheDenominatorInsteadOfScoringTheFloor()
    {
        // Kenna 0 excluido: el denominador baja 8 puntos y el resultado no se castiga.
        var withZero = _engine.Calculate(Empty() with { KennaScore = 0, OperationalScore = 92m });
        var withoutKenna = _engine.Calculate(Empty() with { OperationalScore = 92m });

        Assert.Equal(withoutKenna.Denominator, withZero.Denominator);
        Assert.Equal(withoutKenna.Score, withZero.Score);
    }

    [Fact]
    public void ZeroIsNoData_IsRejectedOnCategoricalMetrics()
    {
        var basis = RulesetCatalog.Default;
        var ruleset = basis with
        {
            Metrics = [.. basis.Metrics.Select(m => m.Key == "mfa" ? m with { ZeroIsNoData = true } : m)],
        };

        var ex = Assert.Throws<RulesetException>(ruleset.Validate);
        Assert.Contains("zeroIsNoData", ex.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------ Casos borde

    [Fact]
    public void SnapshotWithNoDataAtAll_ScoresTheFloor()
    {
        // El BRD nunca produce 0: sin metricas disponibles el score es el piso.
        var result = _engine.Calculate(Empty());

        Assert.Equal(0m, result.Denominator);
        Assert.Equal(20m, result.Score);
        Assert.All(result.Pillars, p => Assert.Null(p.Score));
    }

    [Fact]
    public void PerfectApplication_ScoresOneHundred()
    {
        var snapshot = Empty() with
        {
            KennaScore = 1,
            DisasterRecoveryInScope = true,
            ScaFlaws = new ScaFlawCounts { Critical = 0, High = 0, Medium = 0, Low = 0 },
            UtilizesMfa = true,
            UserCount = 1000,
            OpenSourceScore = 100m,
            RawTechScore = 100m,
            NativeApi = ApiExposure.Apim,
            ArchitecturalValue = new ArchitecturalValue { ModernCount = 10 },
            ProductStrategy = new ProductStrategy { InvestCount = 10 },
            TechDebtScore = 0m,
            Monitoring = new MonitoringCapabilities { ElasticAgent = true },
            OperationalScore = 100m,
        };

        var result = _engine.Calculate(snapshot);

        Assert.Equal(100m, result.Denominator);
        Assert.Equal(100m, result.Score);
    }

    [Fact]
    public void ValueOutsideEveryBand_FailsLoudlyInsteadOfScoringWrong()
    {
        // Un score silenciosamente incorrecto es peor que un error: el motor exige
        // que las bandas del ruleset cubran el valor recibido.
        var snapshot = Empty() with { TechDebtScore = 2.45m };

        var ex = Assert.Throws<RulesetException>(() => _engine.Calculate(snapshot));
        Assert.Contains("techDebt", ex.Message, StringComparison.Ordinal);
    }
}
