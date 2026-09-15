using Modernization.Domain;
using Modernization.Scoring.Engine;

namespace Modernization.WhatIf;

/// <summary>
/// Una palanca es una accion concreta sobre una aplicacion — habilitar MFA,
/// remediar los flaws criticos, incorporarla a APIM — expresada como una
/// transformacion del snapshot.
/// </summary>
/// <remarks>
/// Deliberadamente no calcula ningun delta: solo describe el estado resultante.
/// El delta lo produce el motor de scoring recalculando desde cero, que es la
/// unica forma correcta cuando una palanca puede cambiar el denominador.
/// </remarks>
public sealed record ScenarioLever
{
    public required string Key { get; init; }

    /// <summary>
    /// Nombre de la palanca. Es contenido del catalogo, no una frase generada: va en
    /// ingles desde la Fase 11 (ADR 0017).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Que hay que hacer en la realidad para mover esta palanca.</summary>
    public required string Action { get; init; }

    /// <summary>Metrica que altera. Sirve para explicar el efecto en la UI.</summary>
    public required string MetricKey { get; init; }

    public required string PillarKey { get; init; }

    /// <summary>Transformacion del snapshot. Pura: no muta la entrada.</summary>
    public required Func<ApplicationSnapshot, int?, ApplicationSnapshot> Apply { get; init; }

    /// <summary>
    /// True si la aplicacion ya esta en el estado que la palanca produciria. Una
    /// palanca ya lograda no es una oportunidad: mostrarla como tal seria ruido.
    /// </summary>
    public required Func<ApplicationSnapshot, bool> IsAlreadyAchieved { get; init; }

    /// <summary>Cuantos productos mover, para las palancas que lo admiten.</summary>
    public int? DefaultParameter { get; init; }

    /// <summary>
    /// Por que la palanca no se puede mover en esta aplicacion, mas alla de que ya
    /// este lograda. Null cuando si es accionable.
    /// </summary>
    public Func<ApplicationSnapshot, Notice?>? NotActionableReason { get; init; }

    /// <summary>
    /// Si el laboratorio la ofrece. Una palanca apagada desde la administracion de
    /// reglas no aparece en el catalogo, no cuenta para el techo y no se puede pedir
    /// en un escenario (Fase 12).
    /// </summary>
    public bool Enabled { get; init; } = true;

    public bool IsParameterized => DefaultParameter is not null;
}

/// <summary>Palanca elegida en un escenario, con su parametro si lo tiene.</summary>
/// <param name="Key">Clave de la palanca.</param>
/// <param name="Parameter">Cantidad, para las palancas parametrizadas.</param>
public readonly record struct LeverSelection(string Key, int? Parameter = null);

/// <summary>Lo que se puede ajustar de una palanca desde el portal.</summary>
/// <param name="Key">Clave de la palanca.</param>
/// <param name="Enabled">Si el laboratorio la ofrece.</param>
/// <param name="DefaultParameter">Cantidad por defecto, solo para las parametrizadas.</param>
public sealed record LeverSetting(string Key, bool Enabled, int? DefaultParameter = null);

/// <summary>
/// Que palancas se ofrecen y con que cantidad por defecto. Es la parte del what-if
/// que es dato; la transformacion de cada palanca sigue siendo codigo, porque
/// describe una accion real sobre el snapshot y no un numero.
/// </summary>
public sealed record LeverSettings
{
    /// <summary>Tope de la cantidad por defecto de una palanca parametrizada.</summary>
    public const int MaxParameter = 100;

    public required IReadOnlyList<LeverSetting> Levers { get; init; }

    /// <summary>Todas encendidas, con las cantidades del catalogo.</summary>
    public static LeverSettings Default { get; } = new()
    {
        Levers = [.. LeverCatalog.Blueprints.Select(b => new LeverSetting(b.Key, true, b.DefaultParameter))],
    };

    public LeverSetting? Find(string key)
        => Levers.FirstOrDefault(l => string.Equals(l.Key, key, StringComparison.Ordinal));
}

/// <summary>
/// Catalogo de palancas. Cada una representa una accion que un equipo puede
/// ejecutar de verdad; no hay palancas que solo existan para subir el numero.
/// </summary>
/// <remarks>
/// Desde la Fase 12 el catalogo se construye contra un ruleset. Las palancas que
/// llevan una metrica "a su mejor banda" leen esa banda de las reglas activas: si
/// alguien mueve el limite de la banda alta de OSS de 75 a 80 desde el portal, la
/// palanca pasa a llevar a 80 sin tocar codigo. Antes esos objetivos eran constantes
/// que podian quedar desalineados con el ruleset.
/// </remarks>
public sealed class LeverCatalog
{
    internal static readonly LeverBlueprint[] Blueprints =
    [
        new("mfa.enable", null, static _ => new ScenarioLever
        {
            Key = "mfa.enable",
            Name = "Enable MFA",
            Action = "Integrate the application with the corporate multi-factor provider",
            MetricKey = "mfa",
            PillarKey = "risk",
            Apply = static (s, _) => s with { UtilizesMfa = true },
            IsAlreadyAchieved = static s => s.UtilizesMfa == true || s.UserCount == 0,
        }),
        new("dr.include", null, static _ => new ScenarioLever
        {
            Key = "dr.include",
            Name = "Include in the DR plan",
            Action = "Bring the application into disaster recovery scope",
            MetricKey = "dr.inScope",
            PillarKey = "risk",
            Apply = static (s, _) => s with { DisasterRecoveryInScope = true },
            IsAlreadyAchieved = static s => s.DisasterRecoveryInScope == true,
        }),
        new("sca.remediateCritical", null, static _ => new ScenarioLever
        {
            Key = "sca.remediateCritical",
            Name = "Remediate critical SCA flaws",
            Action = "Upgrade the libraries with Critical-severity vulnerabilities",
            MetricKey = "sca.critical",
            PillarKey = "risk",
            Apply = static (s, _) => s with
            {
                ScaFlaws = (s.ScaFlaws ?? new ScaFlawCounts()) with { Critical = 0 },
            },
            IsAlreadyAchieved = static s => s.ScaFlaws?.Critical == 0,
        }),
        new("sca.remediateHigh", null, static _ => new ScenarioLever
        {
            Key = "sca.remediateHigh",
            Name = "Remediate high SCA flaws",
            Action = "Upgrade the libraries with High-severity vulnerabilities",
            MetricKey = "sca.high",
            PillarKey = "risk",
            Apply = static (s, _) => s with
            {
                ScaFlaws = (s.ScaFlaws ?? new ScaFlawCounts()) with { High = 0 },
            },
            IsAlreadyAchieved = static s => s.ScaFlaws?.High == 0,
        }),
        new("oss.improve", null, static ruleset =>
        {
            var band = new BandTarget(ruleset, "oss");

            return new ScenarioLever
            {
                Key = "oss.improve",
                Name = "Raise the Open-Source Score to the top band",
                Action = "Replace or upgrade the lowest-rated open source components",
                MetricKey = "oss",
                PillarKey = "risk",
                Apply = (s, _) => s with { OpenSourceScore = band.MoveInto(s.OpenSourceScore, upward: true) },
                IsAlreadyAchieved = s => band.Contains(s.OpenSourceScore),
            };
        }),
        new("kenna.reduce", null, static ruleset =>
        {
            var band = new BandTarget(ruleset, "kenna");

            return new ScenarioLever
            {
                Key = "kenna.reduce",
                Name = "Bring the Kenna Score into the top band",
                Action = "Remediate the host vulnerabilities reported by Qualys",
                MetricKey = "kenna",
                PillarKey = "risk",
                Apply = (s, _) => s with { KennaScore = Whole(band.MoveInto(s.KennaScore, upward: false), s.KennaScore) },
                IsAlreadyAchieved = s => band.Contains(s.KennaScore),
            };
        }),
        new("api.onboardApim", null, static _ => new ScenarioLever
        {
            Key = "api.onboardApim",
            Name = "Onboard to APIM",
            Action = "Migrate SOAP services to native REST APIs managed by APIM",
            MetricKey = "api.native",
            PillarKey = "architecture",
            Apply = static (s, _) => s with { NativeApi = ApiExposure.Apim },
            IsAlreadyAchieved = static s => s.NativeApi is ApiExposure.Apim or ApiExposure.Apic,
        }),
        new("rawTech.improve", null, static ruleset =>
        {
            var band = new BandTarget(ruleset, "rawTech");

            return new ScenarioLever
            {
                Key = "rawTech.improve",
                Name = "Raise the RAW Tech Score to the top band",
                Action = "Upgrade the components approaching their end-of-life date",
                MetricKey = "rawTech",
                PillarKey = "architecture",
                Apply = (s, _) => s with { RawTechScore = band.MoveInto(s.RawTechScore, upward: true) },
                IsAlreadyAchieved = s => band.Contains(s.RawTechScore),
            };
        }),
        new("techDebt.clear", null, static _ => new ScenarioLever
        {
            Key = "techDebt.clear",
            Name = "Clear technical debt",
            Action = "Close the debt items recorded in the inventory",
            MetricKey = "techDebt",
            PillarKey = "architecture",
            Apply = static (s, _) => s with { TechDebtScore = 0m },
            IsAlreadyAchieved = static s => s.TechDebtScore == 0m,
        }),
        new("archValue.modernize", 1, static _ => new ScenarioLever
        {
            Key = "archValue.modernize",
            Name = "Modernize products",
            Action = "Replace obsolete or unapproved products with modern equivalents",
            MetricKey = "archValue",
            PillarKey = "architecture",
            DefaultParameter = 1,
            Apply = static (s, count) => ModernizeProducts(s, count ?? 1),
            IsAlreadyAchieved = static s =>
                s.ArchitecturalValue is null
                || s.ArchitecturalValue.ProvidedValue is not null
                || (s.ArchitecturalValue.ObsoleteCount is null or 0
                    && s.ArchitecturalValue.NotApprovedForUseCount is null or 0
                    && s.ArchitecturalValue.HeritageCount is null or 0),
            // La fuente entrega el compuesto ya calculado: no hay conteos que mover.
            NotActionableReason = static s => s.ArchitecturalValue?.ProvidedValue is not null
                ? Notice.Of(NoticeCodes.LeverCompositeProvided, ("system", "CMT"))
                : null,
        }),
        new("productStrategy.invest", 1, static _ => new ScenarioLever
        {
            Key = "productStrategy.invest",
            Name = "Reclassify products to Invest",
            Action = "Move products from Eliminate, Migrate or Tolerate to Invest",
            MetricKey = "productStrategy",
            PillarKey = "architecture",
            DefaultParameter = 1,
            Apply = static (s, count) => ReclassifyToInvest(s, count ?? 1),
            IsAlreadyAchieved = static s =>
                s.ProductStrategy is null
                || s.ProductStrategy.ProvidedValue is not null
                || (s.ProductStrategy.EliminateCount is null or 0
                    && s.ProductStrategy.MigrateCount is null or 0
                    && s.ProductStrategy.TolerateCount is null or 0),
            NotActionableReason = static s => s.ProductStrategy?.ProvidedValue is not null
                ? Notice.Of(NoticeCodes.LeverCompositeProvided, ("system", "MTS"))
                : null,
        }),
        new("monitoring.enable", null, static _ => new ScenarioLever
        {
            Key = "monitoring.enable",
            Name = "Enable monitoring",
            Action = "Deploy the Elastic agent, APM or synthetic monitoring",
            MetricKey = "monitoring",
            PillarKey = "stability",
            Apply = static (s, _) => s with
            {
                Monitoring = (s.Monitoring ?? new MonitoringCapabilities()) with { ElasticAgent = true },
            },
            IsAlreadyAchieved = static s => s.Monitoring?.AnyEnabled == true,
        }),
        new("operational.improve", null, static ruleset =>
        {
            var band = new BandTarget(ruleset, "operational");

            return new ScenarioLever
            {
                Key = "operational.improve",
                Name = "Raise the Operational Score to the top band",
                Action = "Reduce the incidents and problems reported in ServiceNow",
                MetricKey = "operational",
                PillarKey = "stability",
                Apply = (s, _) => s with { OperationalScore = band.MoveInto(s.OperationalScore, upward: true) },
                IsAlreadyAchieved = s => band.Contains(s.OperationalScore),
            };
        }),
    ];

    private static readonly Lazy<LeverCatalog> DefaultCatalog = new(() => new LeverCatalog(RulesetCatalog.Default));

    private readonly ScenarioLever[] _all;
    private readonly ScenarioLever[] _enabled;

    /// <summary>
    /// Arma el catalogo contra un ruleset. Sin ajustes, todas las palancas encendidas
    /// con sus cantidades de fabrica.
    /// </summary>
    /// <exception cref="ArgumentException">Los ajustes nombran una palanca que no existe o una cantidad invalida.</exception>
    public LeverCatalog(Ruleset ruleset, LeverSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(ruleset);

        settings ??= LeverSettings.Default;
        Validate(settings);

        _all = [.. Blueprints.Select(blueprint =>
        {
            var lever = blueprint.Build(ruleset);
            var setting = settings.Find(lever.Key);

            return lever with
            {
                Enabled = setting?.Enabled ?? true,
                DefaultParameter = lever.IsParameterized ? setting?.DefaultParameter ?? lever.DefaultParameter : null,
            };
        })];

        _enabled = [.. _all.Where(l => l.Enabled)];
    }

    /// <summary>El catalogo de fabrica sobre el ruleset v1.3.</summary>
    public static LeverCatalog Default => DefaultCatalog.Value;

    /// <summary>Las claves de todas las palancas que existen, encendidas o no.</summary>
    public static IReadOnlyList<string> Keys { get; } = [.. Blueprints.Select(b => b.Key)];

    /// <summary>Las palancas que el laboratorio ofrece.</summary>
    public IReadOnlyList<ScenarioLever> Levers => _enabled;

    /// <summary>Todas, incluidas las apagadas: es lo que edita la administracion.</summary>
    public IReadOnlyList<ScenarioLever> All => _all;

    /// <summary>Una palanca encendida, o null.</summary>
    public ScenarioLever? Find(string key)
        => Array.Find(_enabled, lever => string.Equals(lever.Key, key, StringComparison.Ordinal));

    /// <summary>Una palanca encendida. Una apagada es, para un escenario, inexistente.</summary>
    public ScenarioLever Require(string key)
        => Find(key) ?? throw new WhatIfException(
            "unknown-lever",
            "One of the scenario's levers does not exist. Valid levers: " +
            $"{string.Join(", ", _enabled.Select(l => l.Key).Order(StringComparer.Ordinal))}.");

    // -----------------------------------------------------------------------

    private static void Validate(LeverSettings settings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var setting in settings.Levers)
        {
            var blueprint = Array.Find(Blueprints, b => string.Equals(b.Key, setting.Key, StringComparison.Ordinal))
                ?? throw new ArgumentException($"La palanca '{setting.Key}' no existe en el catalogo.", nameof(settings));

            if (!seen.Add(setting.Key))
            {
                throw new ArgumentException($"La palanca '{setting.Key}' esta repetida en los ajustes.", nameof(settings));
            }

            var invalidParameter = blueprint.DefaultParameter is null
                ? setting.DefaultParameter is not null
                : setting.DefaultParameter is < 1 or > LeverSettings.MaxParameter;

            if (invalidParameter)
            {
                throw new ArgumentException(
                    $"La cantidad por defecto de la palanca '{setting.Key}' no es valida.",
                    nameof(settings));
            }
        }
    }

    /// <summary>
    /// Kenna es un entero: el borde de la banda se redondea hacia dentro de ella,
    /// hacia arriba si se sube y hacia abajo si se baja.
    /// </summary>
    private static int? Whole(decimal? target, int? current)
        => target is not { } value
            ? current
            : (int)(current is { } before && value > before ? Math.Ceiling(value) : Math.Floor(value));

    /// <summary>
    /// Mueve productos hacia Modern, empezando por los que mas penalizan:
    /// obsoletos y no aprobados puntuan 20, heritage puntua 60.
    /// </summary>
    private static ApplicationSnapshot ModernizeProducts(ApplicationSnapshot snapshot, int count)
    {
        if (snapshot.ArchitecturalValue is not { } value || count <= 0)
        {
            return snapshot;
        }

        var remaining = count;
        var obsolete = Take(value.ObsoleteCount ?? 0, ref remaining);
        var notApproved = Take(value.NotApprovedForUseCount ?? 0, ref remaining);
        var heritage = Take(value.HeritageCount ?? 0, ref remaining);
        var moved = obsolete + notApproved + heritage;

        return snapshot with
        {
            ArchitecturalValue = value with
            {
                ModernCount = (value.ModernCount ?? 0) + moved,
                ObsoleteCount = (value.ObsoleteCount ?? 0) - obsolete,
                NotApprovedForUseCount = (value.NotApprovedForUseCount ?? 0) - notApproved,
                HeritageCount = (value.HeritageCount ?? 0) - heritage,
            },
        };
    }

    /// <summary>
    /// Mueve productos hacia Invest, empezando por Eliminate y Migrate (20) y
    /// despues Tolerate (60).
    /// </summary>
    private static ApplicationSnapshot ReclassifyToInvest(ApplicationSnapshot snapshot, int count)
    {
        if (snapshot.ProductStrategy is not { } strategy || count <= 0)
        {
            return snapshot;
        }

        var remaining = count;
        var eliminate = Take(strategy.EliminateCount ?? 0, ref remaining);
        var migrate = Take(strategy.MigrateCount ?? 0, ref remaining);
        var tolerate = Take(strategy.TolerateCount ?? 0, ref remaining);
        var moved = eliminate + migrate + tolerate;

        return snapshot with
        {
            ProductStrategy = strategy with
            {
                InvestCount = (strategy.InvestCount ?? 0) + moved,
                EliminateCount = (strategy.EliminateCount ?? 0) - eliminate,
                MigrateCount = (strategy.MigrateCount ?? 0) - migrate,
                TolerateCount = (strategy.TolerateCount ?? 0) - tolerate,
            },
        };
    }

    private static int Take(int available, ref int remaining)
    {
        var taken = Math.Min(available, remaining);
        remaining -= taken;
        return taken;
    }
}

/// <summary>Como se construye una palanca contra un ruleset.</summary>
internal sealed record LeverBlueprint(string Key, int? DefaultParameter, Func<Ruleset, ScenarioLever> Build);

/// <summary>
/// La banda de mayor puntaje de una metrica numerica, leida del ruleset. Es el
/// objetivo de las palancas que llevan una metrica "a lo mejor que puede estar".
/// </summary>
internal sealed class BandTarget
{
    private readonly BandDefinition[] _top;

    public BandTarget(Ruleset ruleset, string metricKey)
    {
        var bands = ruleset.Metrics
            .FirstOrDefault(m => string.Equals(m.Key, metricKey, StringComparison.Ordinal))?
            .Mapping.Bands;

        if (bands is not { Count: > 0 })
        {
            _top = [];
            return;
        }

        var best = bands.Max(b => b.Score);
        _top = [.. bands.Where(b => b.Score == best)];
    }

    public bool Contains(decimal? value) => value is { } v && Array.Exists(_top, band => band.Contains(v));

    /// <summary>
    /// El valor mas cercano dentro de la mejor banda. Sin dato, el borde por el que
    /// se entra: el minimo en las metricas que suben, el maximo en las que bajan.
    /// </summary>
    public decimal? MoveInto(decimal? value, bool upward)
    {
        if (_top.Length == 0)
        {
            return value;
        }

        if (value is not { } current)
        {
            return upward ? _top.Min(b => b.Min) : _top.Max(b => b.Max);
        }

        if (Contains(current))
        {
            return current;
        }

        return _top
            .Select(band => current < band.Min ? band.Min : band.Max)
            .MinBy(edge => Math.Abs(edge - current));
    }
}

/// <summary>
/// Escenario invalido: palanca desconocida o parametro fuera de rango. El mensaje va
/// en ingles porque termina en el <c>detail</c> de un ProblemDetails; el codigo es el
/// <c>type</c> estable.
/// </summary>
public sealed class WhatIfException : Exception
{
    private const string DefaultCode = "invalid-scenario";

    public WhatIfException(string code, string message) : base(message) => Code = code;

    public WhatIfException(string message) : this(DefaultCode, message) { }

    public WhatIfException(string message, Exception innerException)
        : base(message, innerException) => Code = DefaultCode;

    public WhatIfException() : this(DefaultCode, "The scenario is not valid.") { }

    /// <summary>Codigo estable del problema, p. ej. <c>unknown-lever</c>.</summary>
    public string Code { get; }
}
