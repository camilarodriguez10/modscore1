using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Modernization.Sources.Dummy;

/// <summary>
/// Genera un portafolio sintetico con la forma de las fuentes reales.
/// </summary>
/// <remarks>
/// Determinista por construccion: la misma semilla produce siempre los mismos
/// datos, asi que los diffs son limpios y los tests reproducibles.
/// <para>
/// El vocabulario (nombres de aplicacion, capacidades de negocio, CVEs,
/// librerias) esta curado a partir de los archivos observados en lugar de
/// generarse al azar. Da datos mas parecidos a los reales que un generador
/// generico y evita una dependencia mas en el arbol.
/// </para>
/// </remarks>
// Random con semilla fija, a proposito: es lo que hace reproducibles los datos
// dummy y los golden tests. Nada de lo que genera es un secreto ni un identificador
// que un atacante gane adivinando.
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Generador determinista de datos de prueba: la reproducibilidad por semilla es el requisito.")]
public sealed class DummyPortfolio
{
    public const int DefaultSeed = 20260910;
    public const int DefaultApplicationCount = 250;

    /// <summary>
    /// EAI de referencia del BRD. Se inyecta con valores exactos para que el
    /// pipeline completo (lectura, ensamblado, scoring) se pueda verificar
    /// extremo a extremo contra un resultado conocido: 80,70 con el ruleset de fabrica, o 79,67
    /// con las bandas de OSS y Raw Tech del BRD (ADR 0032).
    /// </summary>
    public const string ReferenceEaiCode = "14381";

    public required int Seed { get; init; }
    public required DateTimeOffset CapturedAt { get; init; }
    public required IReadOnlyList<EaiRecord> Eai { get; init; }
    public required IReadOnlyList<ArchitecturalValueRecord> ArchitecturalValue { get; init; }
    public required IReadOnlyList<ProductStrategyRecord> ProductStrategy { get; init; }
    public required IReadOnlyList<TechDebtRecord> TechDebt { get; init; }
    public required IReadOnlyList<ScaFlawRecord> ScaFlaws { get; init; }
    public required IReadOnlyList<OssScoreRecord> OssScore { get; init; }
    public required IReadOnlyList<MonitoringRecord> Monitoring { get; init; }
    public required IReadOnlyList<ApimOnboardingRecord> ApimOnboarding { get; init; }

    public string Origin => FormattableString.Invariant($"seed:{Seed}");

    // ------------------------------------------------------------ vocabulario

    private static readonly string[] Countries = ["Chile", "Mexico", "Brazil"];
    private static readonly string[] Regions = ["LATAM"];

    private static readonly string[] Domains =
    [
        "Annuities Sales Process Manager", "Beneficiaries Administration", "Broker Portal",
        "Customer Self-Service", "Partner Web Portal", "Bank Deposit Administration",
        "Data Analytics Hub", "BackOffice Digital", "Unified Customer Database",
        "Claims Intake", "Underwriting Workbench", "Policy Administration",
        "Billing and Collections", "Agent Commissions", "Health Provider Network",
        "Group Benefits Enrollment", "Document Management", "Payments Gateway",
        "Fraud Detection", "Actuarial Reporting", "Quotation Engine",
        "Customer Notification Hub", "Regulatory Reporting", "Reinsurance Ledger",
        "Digital Onboarding", "Contact Center Desktop",
    ];

    private static readonly string[] Lobs =
    [
        "Individual", "Group Benefits", "Retirement & Income Solutions", "Corporate Functions",
    ];

    private static readonly string[] SubLobs =
    [
        "Life", "Health", "Annuities", "Pensions", "Operations", "Finance", "Technology",
    ];

    private static readonly string[] Cios =
    [
        "A. Moreno", "C. Duarte", "L. Ferreira", "M. Salgado", "R. Villalobos",
    ];

    private static readonly string[] Cres =
    [
        "B. Cardoso", "D. Iturra", "E. Navarrete", "F. Quiroga", "G. Alcantara",
        "H. Bustamante", "J. Peralta", "K. Monsalve",
    ];

    private static readonly string[] Languages =
    [
        "Java", "C#", "Python", "JavaScript", "PL/SQL", "COBOL", "ABAP", "Go",
    ];

    private static readonly string[] Statuses = ["Active", "Active", "Active", "In Development", "Sunset"];
    private static readonly string[] CloudTypes = ["PaaS", "IaaS", "SaaS"];
    private static readonly string[] CloudVendors = ["Azure", "AWS", "On-Premise"];
    private static readonly string[] RtoRanges = ["0-4 hrs.", "4-24 hrs.", "24-48 hrs.", "48-72 hrs.", "72+ hrs."];
    private static readonly string[] RpoRanges = ["0-1 hrs.", "1-4 hrs.", "4-24 hrs.", "24+ hrs."];

    private static readonly string[] BusinessCapabilities =
    [
        "Customer Management", "Policy Servicing", "Claims Management", "Distribution",
        "Finance & Actuarial", "Risk & Compliance", "Enterprise Data",
    ];

    /// <summary>Pares disposition/roadmap validos segun MAPS.</summary>
    private static readonly (string Disposition, string Roadmap)[] MapsPairs =
    [
        ("Tolerate", "Maintain (Patch)"),
        ("Tolerate", "Rehost"),
        ("Invest", "Maintain (Evolve)"),
        ("Invest", "Refactor"),
        ("Invest", "Reimagine"),
        ("Eliminate", "Retire"),
        ("Eliminate", "Consolidate"),
    ];

    private static readonly string[] ApiTypes = ["APIM", "APIM", "APIC", "SOAP", "DataPower/SOAP Libraries"];

    // En ingles, como los exporta Veracode: llegan tal cual al future plan (2026-10-21).
    private static readonly (string Library, string Vendor, string Cve, decimal Cvss, string Name)[] Vulnerabilities =
    [
        ("jackson-databind", "FasterXML", "CVE-2018-7489", 9.8m, "Insecure deserialization"),
        ("log4j-core", "Apache", "CVE-2021-44228", 10.0m, "Remote code execution via JNDI lookup"),
        ("spring-web", "VMware", "CVE-2024-22243", 7.5m, "Open redirect"),
        ("tomcat-embed-core", "Apache", "CVE-2025-24813", 9.8m, "Deserialization of persistent sessions"),
        ("commons-io", "Apache", "CVE-2024-47554", 7.5m, "Denial of service through CPU exhaustion"),
        ("struts2-core", "Apache", "CVE-2023-50164", 9.8m, "Path traversal in file upload"),
        ("junit4", "JUnit", "CVE-2020-15250", 5.5m, "Information exposure in the temporary directory"),
        ("snakeyaml", "SnakeYAML", "CVE-2022-1471", 8.3m, "Unsafe YAML deserialization"),
        ("netty-codec-http", "Netty", "CVE-2025-24970", 7.5m, "Denial of service in the TLS handshake"),
        ("okhttp", "Square", "CVE-2023-3635", 7.5m, "Buffer overflow in GzipSource"),
    ];

    private static readonly string[] Severities = ["1-Critical", "2-High", "3-Medium", "4-Low"];

    // -------------------------------------------------------------- generacion

    public static DummyPortfolio Generate(
        int seed = DefaultSeed,
        int applicationCount = DefaultApplicationCount,
        DateTimeOffset? capturedAt = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(applicationCount, 1);

        var random = new Random(seed);
        var captured = capturedAt ?? new DateTimeOffset(2026, 9, 8, 21, 24, 0, TimeSpan.Zero);

        var eai = new List<EaiRecord>(applicationCount);
        var archValue = new List<ArchitecturalValueRecord>();
        var productStrategy = new List<ProductStrategyRecord>();
        var techDebt = new List<TechDebtRecord>();
        var sca = new List<ScaFlawRecord>();
        var oss = new List<OssScoreRecord>();
        var monitoring = new List<MonitoringRecord>();
        var apim = new List<ApimOnboardingRecord>();

        var usedCodes = new HashSet<string> { ReferenceEaiCode };
        long flawIndex = 200_000;

        AppendReferenceApplication(eai, archValue, productStrategy, monitoring, oss, captured);

        for (var i = 0; i < applicationCount - 1; i++)
        {
            var eaiCode = NextEaiCode(random, usedCodes);
            var appId = (1000 + (i * 7)).ToString(CultureInfo.InvariantCulture);
            var country = Pick(random, Countries);
            var domain = Pick(random, Domains);
            var (disposition, roadmap) = Pick(random, MapsPairs);

            eai.Add(new EaiRecord
            {
                EaiCode = eaiCode,
                AppId = appId,
                ApplicationName = FormattableString.Invariant($"{country} - {domain}"),
                ApplicationStatus = Pick(random, Statuses),
                ApplicationDisposition = disposition,
                Roadmap = roadmap,
                Cio = Pick(random, Cios),
                Cre = Pick(random, Cres),
                Lob = Pick(random, Lobs),
                SubLob = Pick(random, SubLobs),
                RegionName = Pick(random, Regions),
                CountryName = country,
                CriticalApplicationIndicator = random.Next(100) < 22 ? "Y" : "N",
                InternetFacing = random.Next(100) < 40 ? "Y" : "N",
                CloudIndicator = random.Next(100) < 55 ? "Yes" : "No",
                CloudType = Pick(random, CloudTypes),
                CloudVendor = Pick(random, CloudVendors),
                CodingLanguage = Pick(random, Languages),

                // 6% sin dato de DR: reproduce el "No Data Available" del dashboard.
                DrInScope = random.Next(100) < 6 ? null : (random.Next(100) < 62 ? "Yes" : "No"),
                UtilizesMfa = random.Next(100) < 4 ? null : (random.Next(100) < 71 ? "Yes" : "No"),

                PrimaryBusinessCapability = Pick(random, BusinessCapabilities),
                L1BusinessCapabilityCount = random.Next(1, 6),
                RecoveryPointObjectiveRange = Pick(random, RpoRanges),
                RecoveryTimeObjectiveRange = Pick(random, RtoRanges),

                // 9% sin usuarios: activa la rama "No (with no users)" de MFA.
                UserCount = random.Next(100) < 9 ? 0 : random.Next(5, 25_000),

                KennaScore = random.Next(100) < 8 ? null : SkewedKenna(random),
                RawTechScore = random.Next(100) < 7 ? null : SkewedRawTech(random),
                OperationalScore = random.Next(100) < 7 ? null : SkewedOperational(random),
            });

            // Composicion arquitectonica — 5% ausente, 4% con todos los conteos en cero.
            if (random.Next(100) >= 5)
            {
                var empty = random.Next(100) < 4;
                archValue.Add(new ArchitecturalValueRecord
                {
                    EaiCode = eaiCode,
                    ModernCount = empty ? 0 : random.Next(0, 60),
                    HeritageCount = empty ? 0 : random.Next(0, 25),
                    ObsoleteCount = empty ? 0 : random.Next(0, 6),
                    NotApprovedForUseCount = empty ? 0 : random.Next(0, 20),
                });
            }

            // Estrategia de producto — 6% ausente.
            if (random.Next(100) >= 6)
            {
                productStrategy.Add(new ProductStrategyRecord
                {
                    EaiCode = eaiCode,
                    InvestCount = random.Next(0, 12),
                    TolerateCount = random.Next(0, 8),
                    EliminateCount = random.Next(0, 4),
                    MigrateCount = random.Next(0, 5),
                });
            }

            // Tech debt — 18% sin fila, que es el caso mas frecuente en el dashboard.
            if (random.Next(100) >= 18)
            {
                // El puntaje combina denegación y aceptación, como en el origen. Los sorteos se
                // conservan aunque solo se guarde el puntaje: cambiar la secuencia cambiaría
                // todo el portafolio de ejemplo.
                var denial = Quantize(random.Next(0, 21) / 4m);
                var accepted = Quantize(random.Next(0, 11) / 4m);
                _ = random.Next(1, 200);
                techDebt.Add(new TechDebtRecord
                {
                    AppId = appId,
                    CombinedAppScore = Quantize((denial + accepted) / 2m),
                });
            }

            // SCA — 14% nunca escaneadas: quedan sin dato, no con cero flaws.
            //
            // Una aplicacion escaneada y limpia igual aparece en el archivo, con sus
            // hallazgos ya resueltos. Sin esas filas seria indistinguible de una que
            // nunca se escaneo, y el caso "None => 100" no se ejercitaria nunca.
            if (random.Next(100) >= 14)
            {
                var openCount = random.Next(100) switch
                {
                    < 30 => 0,
                    < 70 => random.Next(1, 8),
                    _ => random.Next(8, 45),
                };

                var resolvedCount = openCount == 0 ? random.Next(1, 4) : random.Next(0, 3);

                for (var f = 0; f < openCount + resolvedCount; f++)
                {
                    var isOpen = f < openCount;
                    var (library, vendor, cve, cvss, name) = Pick(random, Vulnerabilities);
                    var firstFound = captured.AddDays(-random.Next(30, 2200));

                    sca.Add(new ScaFlawRecord
                    {
                        FlawIndexNumber = ++flawIndex,
                        EaiCode = eaiCode,
                        AppId = appId,
                        ApplicationName = FormattableString.Invariant($"{country} - {domain}"),
                        SeverityDescription = Pick(random, Severities),
                        FindingStatus = isOpen ? "Open" : "Closed",
                        FirstFoundDate = firstFound,
                        LastFoundDate = captured.AddDays(-random.Next(0, 10)),
                        ResolvedDate = isOpen ? null : captured.AddDays(-random.Next(1, 120)),
                        FindingAge = (int)(captured - firstFound).TotalDays,
                        ScanType = "SCA",
                        ReopenFlag = random.Next(100) < 6 ? "Yes" : "No",
                        CveId = cve,
                        Cvssv3Score = cvss,
                        VulnerabilityName = name,
                        LibraryName = library,
                        LibraryVendor = vendor,
                        PublishedDate = firstFound.AddDays(-random.Next(10, 400)),
                        Accessibility = random.Next(100) < 35 ? "External" : "Internal",
                    });
                }
            }

            // OSS — 10% sin fila.
            if (random.Next(100) >= 10)
            {
                oss.Add(new OssScoreRecord
                {
                    EaiCode = eaiCode,
                    OssScore = SkewedOss(random),
                    UpdatedAt = captured,
                });
            }

            // Monitoreo — 12% sin fila.
            if (random.Next(100) >= 12)
            {
                var instrumented = random.Next(100) < 46;
                monitoring.Add(new MonitoringRecord
                {
                    EaiCode = eaiCode,
                    Agent = instrumented && random.Next(100) < 55,
                    Apm = instrumented && random.Next(100) < 62,
                    Monitoring = instrumented && random.Next(100) < 70,
                    Synthetics = instrumented && random.Next(100) < 41,
                });
            }

            // Exposicion de APIs — 26% sin fila: la aplicacion no expone APIs gestionadas.
            if (random.Next(100) >= 26)
            {
                apim.Add(new ApimOnboardingRecord
                {
                    EaiCode = eaiCode,
                    Country = country,
                    ApplicationShortName = FormattableString.Invariant($"{eaiCode}-{Abbreviate(domain)}"),
                    ApplicationName = FormattableString.Invariant($"{country} - {domain}"),
                    ApiType = Pick(random, ApiTypes),
                });
            }
        }

        AppendOrphanRows(oss, monitoring, captured);

        return new DummyPortfolio
        {
            Seed = seed,
            CapturedAt = captured,
            Eai = eai,
            ArchitecturalValue = archValue,
            ProductStrategy = productStrategy,
            TechDebt = techDebt,
            ScaFlaws = sca,
            OssScore = oss,
            Monitoring = monitoring,
            ApimOnboarding = apim,
        };
    }

    /// <summary>
    /// EAI 14381 del BRD, con los valores exactos que producen 79,67 con las bandas del BRD y
    /// 80,70 con OSS y Raw Tech en su valor bruto (ADR 0032).
    /// Deliberadamente ausente de tech_debt, SCA y APIM: esas tres metricas deben
    /// quedar sin dato para que el denominador sea 60 y no 100.
    /// </summary>
    private static void AppendReferenceApplication(
        List<EaiRecord> eai,
        List<ArchitecturalValueRecord> archValue,
        List<ProductStrategyRecord> productStrategy,
        List<MonitoringRecord> monitoring,
        List<OssScoreRecord> oss,
        DateTimeOffset captured)
    {
        eai.Add(new EaiRecord
        {
            EaiCode = ReferenceEaiCode,
            AppId = "9768",
            ApplicationName = "Reference Application - BRD Worked Example",
            ApplicationStatus = "Active",
            ApplicationDisposition = "Tolerate",
            Roadmap = "Maintain (Patch)",
            Cio = Cios[0],
            Cre = Cres[0],
            Lob = Lobs[0],
            SubLob = SubLobs[0],
            RegionName = "LATAM",
            CountryName = "Chile",
            CriticalApplicationIndicator = "N",
            InternetFacing = "N",
            CloudIndicator = "No",
            CloudType = null,
            CloudVendor = "On-Premise",
            CodingLanguage = "Java",
            DrInScope = null,                 // No Data Available
            UtilizesMfa = "Yes",
            PrimaryBusinessCapability = BusinessCapabilities[1],
            L1BusinessCapabilityCount = 2,
            RecoveryPointObjectiveRange = "4-24 hrs.",
            RecoveryTimeObjectiveRange = "48-72 hrs.",
            UserCount = 350,
            KennaScore = 120,
            RawTechScore = 97.67m,
            OperationalScore = 92m,
        });

        archValue.Add(new ArchitecturalValueRecord
        {
            EaiCode = ReferenceEaiCode,
            ModernCount = 15,
            HeritageCount = 1,
            ObsoleteCount = 0,
            NotApprovedForUseCount = 0,
        });

        productStrategy.Add(new ProductStrategyRecord
        {
            EaiCode = ReferenceEaiCode,
            InvestCount = 2,
            TolerateCount = 1,
            EliminateCount = 1,
            MigrateCount = 0,
        });

        monitoring.Add(new MonitoringRecord
        {
            EaiCode = ReferenceEaiCode,
            Agent = false,
            Apm = false,
            Monitoring = false,
            Synthetics = false,
        });

        oss.Add(new OssScoreRecord
        {
            EaiCode = ReferenceEaiCode,
            OssScore = 28.95m,
            UpdatedAt = captured,
        });
    }

    /// <summary>
    /// Filas cuyo EAI no existe en el maestro. Son un problema real de datos y
    /// deben aparecer como rechazos en la pantalla de linaje en vez de perderse.
    /// </summary>
    private static void AppendOrphanRows(
        List<OssScoreRecord> oss,
        List<MonitoringRecord> monitoring,
        DateTimeOffset captured)
    {
        oss.Add(new OssScoreRecord { EaiCode = "99001", OssScore = 41m, UpdatedAt = captured });
        oss.Add(new OssScoreRecord { EaiCode = "99002", OssScore = 88m, UpdatedAt = captured });
        monitoring.Add(new MonitoringRecord { EaiCode = "99003", Agent = true, Apm = true, Monitoring = true, Synthetics = false });
    }

    // ------------------------------------------------------------------ helpers

    private static T Pick<T>(Random random, T[] options) => options[random.Next(options.Length)];

    private static string NextEaiCode(Random random, HashSet<string> used)
    {
        string code;
        do
        {
            code = random.Next(1000, 15_000).ToString(CultureInfo.InvariantCulture);
        }
        while (!used.Add(code));

        return code;
    }

    /// <summary>Sesga hacia riesgo bajo y medio, como el portafolio observado.</summary>
    private static int SkewedKenna(Random random) => random.Next(100) switch
    {
        < 46 => random.Next(0, 340),
        < 78 => random.Next(340, 670),
        _ => random.Next(670, 1001),
    };

    /// <summary>
    /// El archivo OSS observado concentra los valores en el rango bajo, con
    /// bastantes exactamente en 20 y algunos casos perfectos en 100.
    /// </summary>
    private static decimal SkewedOss(Random random) => random.Next(100) switch
    {
        < 34 => 20m,
        < 72 => random.Next(21, 60),
        < 88 => random.Next(60, 75),
        < 97 => random.Next(75, 100),
        _ => 100m,
    };

    /// <summary>
    /// Las bandas del BRD para RAW Tech Score (0-59, 60-74, 75-100) son enteras.
    /// El generador respeta esa granularidad en lugar de producir valores que
    /// caerian en los huecos entre bandas.
    /// </summary>
    private static decimal SkewedRawTech(Random random) => random.Next(100) switch
    {
        < 18 => random.Next(0, 60),
        < 42 => random.Next(60, 75),
        _ => random.Next(75, 101),
    };

    /// <summary>
    /// Las bandas del BRD para Operational Score dejan huecos entre 59,9 y 60 y
    /// entre 79,99 y 80 (RFC-08). El generador produce solo valores dentro de las
    /// bandas documentadas; un valor en el hueco haria fallar el calculo a proposito.
    /// </summary>
    private static decimal SkewedOperational(Random random) => random.Next(100) switch
    {
        < 14 => Math.Round(random.Next(0, 600) / 10m, 1),
        < 44 => Math.Round(random.Next(6000, 8000) / 100m, 2),
        _ => random.Next(80, 101),
    };

    /// <summary>tech_debt reporta en pasos de 0,25.</summary>
    private static decimal Quantize(decimal value) => Math.Round(value * 4m, MidpointRounding.AwayFromZero) / 4m;

    private static string Abbreviate(string domain)
        => string.Concat(domain.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0])));
}
