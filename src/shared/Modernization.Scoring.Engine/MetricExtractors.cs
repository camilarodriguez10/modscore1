using System.Globalization;
using Modernization.Domain;

namespace Modernization.Scoring.Engine;

/// <summary>
/// Valor crudo de una metrica tal como sale del snapshot, antes de mapearse a
/// la escala 0-100.
/// </summary>
/// <param name="Raw">Valor crudo, para trazabilidad.</param>
/// <param name="Display">Como se lee el valor crudo, como codigo y datos (ADR 0017).</param>
/// <param name="HasData">False si la metrica debe excluirse del calculo.</param>
/// <param name="Missing">Por que falta el dato; null si hay dato.</param>
public readonly record struct MetricInput(
    object? Raw,
    Notice Display,
    bool HasData,
    Notice? Missing)
{
    public static MetricInput NoData(Notice reason)
        => new(null, Notice.Of(NoticeCodes.ValueNoData), false, reason);

    public static MetricInput Present(object raw, Notice display) => new(raw, display, true, null);

    /// <summary>Un valor que se muestra tal cual: un numero, "Yes", "APIM".</summary>
    public static MetricInput Plain(object raw, string value)
        => Present(raw, Notice.Of(NoticeCodes.ValuePlain, ("value", value)));
}

/// <summary>
/// Lee valores crudos del snapshot. La extraccion es tipada (C#) mientras que el
/// mapeo a puntaje es dato (ruleset): lo que cambia con el negocio vive en JSON,
/// lo que depende de la forma del modelo vive en codigo.
/// </summary>
/// <remarks>
/// Desde la Fase 11 no redacta frases: el valor y el motivo de exclusion salen como
/// <see cref="Notice"/>, y la interfaz los lee en su idioma (ADR 0017).
/// </remarks>
public static class MetricExtractors
{
    private static readonly IReadOnlyDictionary<string, Func<ApplicationSnapshot, MetricDefinition, MetricInput>>
        Registry = new Dictionary<string, Func<ApplicationSnapshot, MetricDefinition, MetricInput>>(StringComparer.Ordinal)
        {
            ["kennaScore"] = static (s, m) => FromNumber(s.KennaScore, m, "CMT"),
            ["disasterRecoveryInScope"] = static (s, _) => FromBoolean(s.DisasterRecoveryInScope, "Yes", "No", "EAI"),
            ["scaCritical"] = static (s, _) => FromScaSeverity(s.ScaFlaws?.Critical, "Critical"),
            ["scaHigh"] = static (s, _) => FromScaSeverity(s.ScaFlaws?.High, "High"),
            ["scaMedium"] = static (s, _) => FromScaSeverity(s.ScaFlaws?.Medium, "Medium"),
            ["scaLow"] = static (s, _) => FromScaSeverity(s.ScaFlaws?.Low, "Low"),
            ["mfa"] = static (s, _) => ExtractMfa(s),
            ["openSourceScore"] = static (s, m) => FromNumber(s.OpenSourceScore, m, "RPT"),
            ["rawTechScore"] = static (s, m) => FromNumber(s.RawTechScore, m, "CMT"),
            ["nativeApi"] = static (s, _) => ExtractNativeApi(s),
            ["architecturalValue"] = static (s, m) => ExtractArchitecturalValue(s, m),
            ["productStrategy"] = static (s, m) => ExtractProductStrategy(s, m),
            ["techDebtScore"] = static (s, m) => FromNumber(s.TechDebtScore, m, "EAI"),
            ["automationDevSecOps"] = static (s, m) => FromNumber(s.AutomationDevSecOpsScore, m, string.Empty),
            ["containerized"] = static (s, _) => ExtractContainerized(s),
            ["monitoring"] = static (s, _) => ExtractMonitoring(s),
            ["operationalScore"] = static (s, m) => FromNumber(s.OperationalScore, m, "CMT"),
            ["roadmap"] = static (s, _) => ExtractRoadmap(s),
        };

    /// <summary>Claves de extractor reconocidas. Un ruleset no puede referenciar otras.</summary>
    public static IReadOnlyCollection<string> KnownInputs => (IReadOnlyCollection<string>)Registry.Keys;

    public static MetricInput Extract(string input, ApplicationSnapshot snapshot, MetricDefinition metric)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(metric);

        if (!Registry.TryGetValue(input, out var extractor))
        {
            throw new RulesetException(
                $"La metrica '{metric.Key}' declara el input '{input}', que no existe. " +
                $"Inputs validos: {string.Join(", ", Registry.Keys.Order(StringComparer.Ordinal))}.");
        }

        return extractor(snapshot, metric);
    }

    // -----------------------------------------------------------------------

    /// <summary>La fuente no entrego el dato. <paramref name="system"/> vacio si no hay una concreta.</summary>
    private static Notice NotReported(string system)
        => Notice.Of(NoticeCodes.ExclusionNotReported, ("system", system));

    /// <summary>
    /// Un valor numerico crudo. Si el ruleset declara <see cref="MetricDefinition.ZeroIsNoData"/>,
    /// un 0 se excluye como un nulo (BRD, "ALL Sections"), con su propio motivo para
    /// que la pantalla de explicacion no lo confunda con un dato no reportado.
    /// </summary>
    private static MetricInput FromNumber<T>(T? value, MetricDefinition metric, string system)
        where T : struct, IFormattable
    {
        if (value is null)
        {
            return MetricInput.NoData(NotReported(system));
        }

        if (metric.ZeroIsNoData && EqualityComparer<T>.Default.Equals(value.Value, default))
        {
            return MetricInput.NoData(Notice.Of(NoticeCodes.ExclusionZeroValue, ("system", system)));
        }

        return MetricInput.Plain(value.Value, value.Value.ToString(null, CultureInfo.InvariantCulture));
    }

    private static MetricInput FromBoolean(bool? value, string whenTrue, string whenFalse, string system)
        => value is null
            ? MetricInput.NoData(NotReported(system))
            : MetricInput.Plain(value.Value ? whenTrue : whenFalse, value.Value ? whenTrue : whenFalse);

    /// <summary>
    /// Un conteo de 0 significa "None" y puntua 100; solo la ausencia de dato excluye
    /// la metrica. La distincion importa: confundirlas invertiria el resultado.
    /// </summary>
    private static MetricInput FromScaSeverity(int? count, string severity)
    {
        if (count is null)
        {
            return MetricInput.NoData(Notice.Of(NoticeCodes.ExclusionScaSeverity, ("severity", severity)));
        }

        var dataPoint = count.Value > 0 ? "Present" : "None";

        return MetricInput.Present(
            dataPoint,
            Notice.Of(NoticeCodes.ValueScaFlaws, ("severity", severity), ("count", count.Value)));
    }

    /// <summary>
    /// MFA depende de dos entradas (BRD): solo "No (with users)" penaliza. Una
    /// aplicacion sin usuarios no se castiga por no tener MFA.
    /// Si el conteo de usuarios se desconoce se asume "with users", que es el
    /// supuesto conservador en seguridad.
    /// </summary>
    private static MetricInput ExtractMfa(ApplicationSnapshot snapshot)
    {
        if (snapshot.UtilizesMfa is null)
        {
            return MetricInput.NoData(NotReported("EAI"));
        }

        var hasUsers = snapshot.UserCount is null || snapshot.UserCount > 0;
        var dataPoint = (snapshot.UtilizesMfa.Value, hasUsers) switch
        {
            (true, true) => "Yes (with users)",
            (true, false) => "Yes (with no users)",
            (false, true) => "No (with users)",
            (false, false) => "No (with no users)",
        };

        // Sin conteo de usuarios, el argumento va vacio y la interfaz lo dice.
        return MetricInput.Present(
            dataPoint,
            Notice.Of(NoticeCodes.ValueMfa, ("state", dataPoint), ("users", snapshot.UserCount)));
    }

    private static MetricInput ExtractNativeApi(ApplicationSnapshot snapshot)
    {
        if (snapshot.NativeApi is null)
        {
            return MetricInput.NoData(NotReported("API Management / DataPower"));
        }

        var display = snapshot.NativeApi.Value switch
        {
            ApiExposure.Apim => "APIM",
            ApiExposure.Apic => "APIC",
            ApiExposure.Soap => "SOAP",
            ApiExposure.DataPowerSoapLibraries => "DataPower/SOAP Libraries",
            _ => snapshot.NativeApi.Value.ToString(),
        };

        return MetricInput.Plain(display, display);
    }

    private static MetricInput ExtractContainerized(ApplicationSnapshot snapshot)
        // BRD: "No" no puntua (NULL), solo "Yes" produce un valor.
        => snapshot.Containerized is true
            ? MetricInput.Plain("Yes", "Yes")
            : MetricInput.NoData(Notice.Of(NoticeCodes.ExclusionContainerizedNo));

    private static MetricInput ExtractMonitoring(ApplicationSnapshot snapshot)
    {
        var any = snapshot.Monitoring?.AnyEnabled;
        if (any is null)
        {
            return MetricInput.NoData(Notice.Of(NoticeCodes.ExclusionMonitoring));
        }

        var m = snapshot.Monitoring!;
        var enabled = new List<string>(4);
        if (m.ElasticAgent == true) { enabled.Add("Elastic Agent"); }
        if (m.SyntheticMonitoring == true) { enabled.Add("Synthetics"); }
        if (m.ApplicationPerformanceMonitoring == true) { enabled.Add("APM"); }
        if (m.GeneralMonitoring == true) { enabled.Add("Monitoring"); }

        var dataPoint = any.Value ? "Enabled" : "None";
        var display = enabled.Count > 0
            ? Notice.Of(NoticeCodes.ValueMonitoring, ("capabilities", string.Join(" + ", enabled)))
            : Notice.Of(NoticeCodes.ValueMonitoringNone);

        return MetricInput.Present(dataPoint, display);
    }

    private static MetricInput ExtractRoadmap(ApplicationSnapshot snapshot)
    {
        if (snapshot.Roadmap is null)
        {
            return MetricInput.NoData(NotReported("EAI"));
        }

        var display = MapsNames.Display(snapshot.Roadmap.Value);

        return MetricInput.Plain(display, display);
    }

    // -----------------------------------------------------------------------
    // Compuestos: el valor entregado por la fuente gana sobre los conteos.
    // -----------------------------------------------------------------------

    private static MetricInput ExtractArchitecturalValue(ApplicationSnapshot snapshot, MetricDefinition metric)
    {
        var av = snapshot.ArchitecturalValue;
        if (av is null)
        {
            return MetricInput.NoData(NotReported("CMT"));
        }

        if (av.ProvidedValue is { } provided)
        {
            return MetricInput.Present(provided, Provided(provided, "CMT"));
        }

        if (av.TotalCount == 0)
        {
            return MetricInput.NoData(Notice.Of(NoticeCodes.ExclusionCompositeEmpty, ("system", "CMT")));
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["modern"] = av.ModernCount ?? 0,
            ["heritage"] = av.HeritageCount ?? 0,
            ["obsolete"] = av.ObsoleteCount ?? 0,
            ["notApprovedForUse"] = av.NotApprovedForUseCount ?? 0,
        };

        return WeightedAverage(counts, metric);
    }

    private static MetricInput ExtractProductStrategy(ApplicationSnapshot snapshot, MetricDefinition metric)
    {
        var ps = snapshot.ProductStrategy;
        if (ps is null)
        {
            return MetricInput.NoData(NotReported("MTS"));
        }

        if (ps.ProvidedValue is { } provided)
        {
            return MetricInput.Present(provided, Provided(provided, "MTS"));
        }

        if (ps.TotalCount == 0)
        {
            return MetricInput.NoData(Notice.Of(NoticeCodes.ExclusionCompositeEmpty, ("system", "MTS")));
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["invest"] = ps.InvestCount ?? 0,
            ["tolerate"] = ps.TolerateCount ?? 0,
            ["eliminate"] = ps.EliminateCount ?? 0,
            ["migrate"] = ps.MigrateCount ?? 0,
        };

        return WeightedAverage(counts, metric);
    }

    private static Notice Provided(decimal value, string system)
        => Notice.Of(NoticeCodes.ValueCompositeProvided, ("value", Format(value)), ("system", system));

    private static MetricInput WeightedAverage(Dictionary<string, int> counts, MetricDefinition metric)
    {
        if (metric.Composite is null)
        {
            throw new RulesetException(
                $"La metrica compuesta '{metric.Key}' no declara 'composite.categoryScores'.");
        }

        decimal total = 0;
        decimal accumulated = 0;
        var parts = new List<string>(counts.Count);

        foreach (var (category, count) in counts)
        {
            if (!metric.Composite.CategoryScores.TryGetValue(category, out var categoryScore))
            {
                throw new RulesetException(
                    $"La metrica '{metric.Key}' no declara puntaje para la categoria '{category}'.");
            }

            total += count;
            accumulated += count * categoryScore;

            if (count > 0)
            {
                parts.Add(string.Create(CultureInfo.InvariantCulture, $"{count}x{category}"));
            }
        }

        var value = accumulated / total;

        // Los conteos viajan como dato ("15xmodern + 1xheritage"): las categorias son
        // claves del ruleset, y ponerles nombre es trabajo de la interfaz.
        return MetricInput.Present(
            value,
            Notice.Of(NoticeCodes.ValueCompositeCounts, ("value", Format(value)), ("counts", string.Join(" + ", parts))));
    }

    private static string Format(decimal value)
        => Math.Round(value, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
