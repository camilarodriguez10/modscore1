using System.Diagnostics.CodeAnalysis;
using System.Text;
using Modernization.Domain;
using Modernization.Domain.Code;
using Modernization.Domain.Sources;

namespace Modernization.Sources.Dummy;

/// <summary>
/// Repositorios de demostración para la pantalla Code, mientras no exista la integración con
/// GitHub Enterprise (ADR 0026). Se generan a partir del portafolio vigente, así que
/// funcionan igual con datos generados y con archivos cargados.
/// </summary>
/// <remarks>
/// <para>
/// Son deterministas: la misma aplicación produce siempre los mismos repositorios, con una
/// semilla que sale de su código EAI y no del proceso. El lenguaje del maestro EAI elige la
/// familia de tecnologías, y el Currency Score de CMT inclina las versiones: una plataforma
/// legada tiende a runtimes vencidos, una vigente a los actuales. Así la pantalla cuenta
/// una historia coherente con el resto del portafolio, con las discrepancias justas.
/// </para>
/// <para>
/// Las versiones "últimas" y los conteos de alertas son ilustrativos. Las fechas de fin de
/// vida, en cambio, son reales: salen del catálogo de endoflife.date. El proceso no arranca
/// con este proveedor fuera de Development, para que un dato de demostración nunca se
/// presente como real.
/// </para>
/// </remarks>
[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Generador determinista de datos de demostracion: la reproducibilidad por semilla es el requisito.")]
public sealed class SampleCodeInventoryProvider(IApplicationSnapshotProvider snapshots) : ICodeInventoryProvider, IDisposable
{
    public const string Name = "Sample";

    private const decimal LegacyBelow = 60m;
    private const decimal CurrentFrom = 80m;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private (string VersionId, CodeInventorySet Inventory)? _cache;

    public string Provider => Name;

    public void Dispose() => _gate.Dispose();

    public async Task<CodeInventorySet> GetAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await snapshots.GetAllAsync(cancellationToken).ConfigureAwait(false);

        if (_cache is { } cached && cached.VersionId == snapshot.Version.Id)
        {
            return cached.Inventory;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_cache is { } again && again.VersionId == snapshot.Version.Id)
            {
                return again.Inventory;
            }

            var inventory = new CodeInventorySet
            {
                Provider = Name,
                Connected = false,
                ScannedAt = snapshot.CapturedAt,
                Repositories = Generate(snapshot.Applications, DateOnly.FromDateTime(snapshot.CapturedAt.UtcDateTime)),
            };

            _cache = (snapshot.Version.Id, inventory);

            return inventory;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Los repositorios de todo el portafolio, en el orden del maestro.</summary>
    public static IReadOnlyList<RepositoryInventory> Generate(IReadOnlyList<ApplicationSnapshot> applications, DateOnly scannedOn)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var used = new HashSet<string>(StringComparer.Ordinal);
        var repositories = new List<RepositoryInventory>();

        foreach (var application in applications)
        {
            repositories.AddRange(ForApplication(application, scannedOn, used));
        }

        return repositories;
    }

    // ------------------------------------------------------------ por aplicación

    private enum Era
    {
        Legacy,
        Mixed,
        Current,
    }

    private static List<RepositoryInventory> ForApplication(ApplicationSnapshot application, DateOnly scannedOn, HashSet<string> used)
    {
        var random = new Random(Seed(application.EaiCode));
        var era = application.RawTechScore switch
        {
            > 0m and < LegacyBelow => Era.Legacy,
            >= CurrentFrom => Era.Current,
            _ => Era.Mixed,
        };

        // Las discrepancias justas con CMT: algunas legadas ya se modernizaron en el código
        // y el inventario no se enteró; algunas vigentes arrastran componentes vencidos.
        era = (era, random.NextDouble()) switch
        {
            (Era.Legacy, < 0.12) => Era.Current,
            (Era.Current, < 0.08) => Era.Legacy,
            _ => era,
        };

        // La organización, por las dos primeras palabras de la línea de negocio: retirement-income.
        var org = string.Join('-', Slug(application.Lob ?? "platform", 60).Split('-').Take(2));
        var slug = Slug(application.ApplicationName, 40);
        var repositories = new List<RepositoryInventory>();

        void Add(string suffix, string? language, List<RepositoryComponent> components, List<RepositoryDependency> dependencies)
        {
            var name = $"{org}/{slug}-{suffix}";

            if (!used.Add(name))
            {
                name = $"{org}/{slug}-{application.EaiCode}-{suffix}";
                used.Add(name);
            }

            var archived = random.NextDouble() < (era == Era.Legacy ? 0.06 : 0.02);
            var daysAgo = era switch
            {
                Era.Legacy => random.Next(20, 1100),
                Era.Current => random.Next(1, 200),
                _ => random.Next(1, 600),
            };

            repositories.Add(new RepositoryInventory
            {
                EaiCode = application.EaiCode,
                Name = name,
                DefaultBranch = era == Era.Legacy && random.NextDouble() < 0.6 ? "master" : "main",
                PrimaryLanguage = language,
                LastCommit = scannedOn.AddDays(-daysAgo),
                Archived = archived,
                Components = components,
                Dependencies = dependencies,
            });
        }

        switch (application.CodingLanguage)
        {
            case "Java":
                Add("api", "Java", JavaComponents(random, era), Dependencies(random, era, Maven));
                AddFrontend(random, era, Add);
                if (random.NextDouble() < 0.25)
                {
                    Add("batch", "Java", JavaComponents(random, era, withFramework: false), Dependencies(random, era, Maven, 2, 4));
                }

                break;

            case "C#":
                var framework = era == Era.Legacy || (era == Era.Mixed && random.NextDouble() < 0.5);
                Add("api", "C#", DotNetComponents(random, era, framework), Dependencies(random, era, NuGet));
                AddFrontend(random, era, Add);
                if (random.NextDouble() < 0.25)
                {
                    Add("batch", "C#", DotNetComponents(random, era, framework), Dependencies(random, era, NuGet, 2, 4));
                }

                break;

            case "JavaScript":
                Add("web", "TypeScript", NodeComponents(random, era), Dependencies(random, era, Npm));
                if (random.NextDouble() < 0.4)
                {
                    Add("bff", "JavaScript", NodeComponents(random, era, withFramework: false), Dependencies(random, era, Npm, 3, 5));
                }

                break;

            case "Python":
                Add("service", "Python", PythonComponents(random, era), Dependencies(random, era, PyPi));
                break;

            case "Go":
                Add("service", "Go", GoComponents(random, era), Dependencies(random, era, GoModules, 3, 5));
                break;

            case "PL/SQL":
                if (random.NextDouble() < 0.7)
                {
                    Add("db", "PLSQL", [Component(random, era, Oracle)], []);
                }

                break;

            default:
                // COBOL y ABAP casi nunca viven en GitHub, y cuando viven no declaran
                // manifiestos: la pantalla los muestra como fuera del alcance del análisis.
                if (random.NextDouble() < 0.15)
                {
                    Add("src", application.CodingLanguage, [], []);
                }

                break;
        }

        return repositories;
    }

    private static void AddFrontend(
        Random random,
        Era era,
        Action<string, string?, List<RepositoryComponent>, List<RepositoryDependency>> add)
    {
        if (random.NextDouble() < 0.45)
        {
            add("web", "TypeScript", NodeComponents(random, era), Dependencies(random, era, Npm, 3, 5));
        }
    }

    // ------------------------------------------------------------ componentes

    /// <summary>Un producto con las versiones que suele tener una plataforma legada y una vigente.</summary>
    private sealed record Pool(string Product, string DetectedIn, string[] Old, string[] Current);

    private static readonly Pool OracleJdk = new("oracle-jdk", "pom.xml (maven.compiler.release)",
        ["1.8.0_392", "11.0.22"], ["17.0.12", "21.0.4", "25.0.1"]);
    private static readonly Pool Temurin = new("eclipse-temurin", "Dockerfile (FROM eclipse-temurin)",
        ["1.8.0_402", "11.0.24"], ["17.0.12", "21.0.4", "25.0.1"]);
    private static readonly Pool SpringBoot = new("spring-boot", "pom.xml (spring-boot-starter-parent)",
        ["2.3.12", "2.7.18", "3.3.5"], ["3.5.3", "4.0.1", "4.1.0"]);
    private static readonly Pool SpringFramework = new("spring-framework", "pom.xml (spring-core)",
        ["4.3.30", "5.3.31", "6.1.14"], ["6.2.8", "7.0.3"]);
    private static readonly Pool Tomcat = new("tomcat", "Dockerfile (FROM tomcat)",
        ["7.0.109", "8.5.100"], ["9.0.98", "10.1.34"]);
    private static readonly Pool Ubuntu = new("ubuntu", "Dockerfile (FROM ubuntu)",
        ["16.04", "18.04", "20.04"], ["22.04", "24.04"]);
    private static readonly Pool Oracle = new("oracle-database", "flyway.conf",
        ["11.2", "12.1", "12.2"], ["19", "21", "23"]);
    private static readonly Pool Postgres = new("postgresql", "docker-compose.yml",
        ["9.6", "11", "12"], ["15", "16", "17"]);
    private static readonly Pool DotNet = new("dotnet", "global.json",
        ["3.1.32", "5.0.17", "6.0.36", "7.0.20"], ["8.0.11", "9.0.6", "10.0.0"]);
    private static readonly Pool DotNetFramework = new("dotnetfx", "*.csproj (TargetFrameworkVersion)",
        ["4.5.2", "4.6.1", "4.6.2"], ["4.7.2", "4.8", "4.8.1"]);
    private static readonly Pool WindowsServer = new("windows-server", "deploy/servers.yml",
        ["2012-r2", "2016"], ["2019", "2022"]);
    private static readonly Pool SqlServer = new("mssqlserver", "database/*.sqlproj",
        ["11.0-sp4", "12.0-sp3", "13.0-sp3"], ["14.0", "15.0", "16.0"]);
    private static readonly Pool Node = new("nodejs", ".nvmrc",
        ["12.22.12", "14.21.3", "16.20.2", "18.20.4", "20.18.0"], ["22.11.0", "24.4.1"]);
    private static readonly Pool AngularJs = new("angularjs", "package.json (angular)",
        ["1.5.11", "1.6.10", "1.7.9", "1.8.3"], ["1.8.3"]);
    private static readonly Pool Angular = new("angular", "package.json (@angular/core)",
        ["8.2.14", "11.2.14", "13.4.0", "15.2.10", "17.3.12"], ["20.1.0", "21.0.3", "22.0.1"]);
    private static readonly Pool Vue = new("vue", "package.json (vue)",
        ["2.5.22", "2.6.14", "2.7.16", "3.4.38"], ["3.5.13"]);
    private static readonly Pool React = new("react", "package.json (react)",
        ["16.14.0", "17.0.2"], ["18.3.1", "19.1.0"]);
    private static readonly Pool JQuery = new("jquery", "package.json (jquery)",
        ["1.12.4", "2.2.4"], ["3.7.1"]);
    private static readonly Pool Python = new("python", ".python-version",
        ["2.7.18", "3.6.15", "3.7.17", "3.8.20"], ["3.10.15", "3.11.10", "3.12.7", "3.13.1"]);
    private static readonly Pool Django = new("django", "requirements.txt (Django)",
        ["1.11.29", "2.2.28", "3.2.25", "4.2.16"], ["5.2.4", "6.0.1"]);
    private static readonly Pool Go = new("go", "go.mod",
        ["1.19.13", "1.21.13", "1.24.6"], ["1.26.2", "1.27.0"]);
    private static readonly Pool Log4j = new("log4j", "pom.xml (log4j)", ["1.2.17", "2.12.4"], ["2.24.3"]);

    /// <summary>Fuera del catálogo a propósito: la pantalla dice que no lo puede evaluar.</summary>
    private static readonly Pool WebSphere = new("websphere", "server.xml", ["8.5.5.24"], ["9.0.5.20"]);

    private static RepositoryComponent Component(Random random, Era era, Pool pool)
    {
        var oldChance = era switch
        {
            Era.Legacy => 0.8,
            Era.Current => 0.1,
            _ => 0.35,
        };

        var versions = random.NextDouble() < oldChance ? pool.Old : pool.Current;

        return new RepositoryComponent(pool.Product, versions[random.Next(versions.Length)], pool.DetectedIn);
    }

    private static List<RepositoryComponent> JavaComponents(Random random, Era era, bool withFramework = true)
    {
        var components = new List<RepositoryComponent> { Component(random, era, random.NextDouble() < 0.5 ? OracleJdk : Temurin) };

        if (withFramework)
        {
            components.Add(Component(random, era, random.NextDouble() < 0.6 ? SpringBoot : SpringFramework));
        }

        if (era == Era.Legacy && random.NextDouble() < 0.35)
        {
            components.Add(Component(random, era, Log4j));
        }

        if (random.NextDouble() < 0.35)
        {
            components.Add(Component(random, era, era == Era.Legacy && random.NextDouble() < 0.4 ? WebSphere : Tomcat));
        }

        if (random.NextDouble() < 0.6)
        {
            components.Add(Component(random, era, Ubuntu));
        }

        if (random.NextDouble() < 0.6)
        {
            components.Add(Component(random, era, random.NextDouble() < 0.5 ? Oracle : Postgres));
        }

        return components;
    }

    private static List<RepositoryComponent> DotNetComponents(Random random, Era era, bool framework)
    {
        if (framework)
        {
            return
            [
                Component(random, era, DotNetFramework),
                Component(random, era, WindowsServer),
                Component(random, era, SqlServer),
            ];
        }

        var components = new List<RepositoryComponent> { Component(random, era, DotNet) };

        if (random.NextDouble() < 0.5)
        {
            components.Add(Component(random, era, Ubuntu));
        }

        if (random.NextDouble() < 0.6)
        {
            components.Add(Component(random, era, SqlServer));
        }

        return components;
    }

    private static List<RepositoryComponent> NodeComponents(Random random, Era era, bool withFramework = true)
    {
        var components = new List<RepositoryComponent> { Component(random, era, Node) };

        if (withFramework)
        {
            var legacy = era == Era.Legacy || (era == Era.Mixed && random.NextDouble() < 0.4);
            var pool = legacy
                ? (random.NextDouble() < 0.55 ? AngularJs : random.NextDouble() < 0.5 ? JQuery : Vue)
                : (random.NextDouble() < 0.45 ? Angular : random.NextDouble() < 0.6 ? React : Vue);

            components.Add(Component(random, era, pool));
        }

        if (random.NextDouble() < 0.4)
        {
            components.Add(Component(random, era, Ubuntu));
        }

        return components;
    }

    private static List<RepositoryComponent> PythonComponents(Random random, Era era)
    {
        var components = new List<RepositoryComponent> { Component(random, era, Python) };

        if (random.NextDouble() < 0.5)
        {
            components.Add(Component(random, era, Django));
        }

        if (random.NextDouble() < 0.6)
        {
            components.Add(Component(random, era, Ubuntu));
        }

        if (random.NextDouble() < 0.5)
        {
            components.Add(Component(random, era, Postgres));
        }

        return components;
    }

    private static List<RepositoryComponent> GoComponents(Random random, Era era)
    {
        var components = new List<RepositoryComponent> { Component(random, era, Go) };

        if (random.NextDouble() < 0.3)
        {
            components.Add(Component(random, era, Ubuntu));
        }

        return components;
    }

    // ------------------------------------------------------------ dependencias

    /// <summary>
    /// Una biblioteca con una versión típica de una plataforma legada y otra vigente. Null en
    /// una de las dos es que ese tipo de plataforma no la usa.
    /// </summary>
    private sealed record Library(
        string Name,
        string? Old,
        string? Current,
        string Latest,
        bool DeprecatedOld = false,
        int OldCritical = 0,
        int OldHigh = 0,
        int OldMedium = 0,
        int CurrentMedium = 0);

    private sealed record Ecosystem(string Name, Library[] Libraries);

    private static readonly Ecosystem Maven = new("Maven",
    [
        new("com.fasterxml.jackson.core:jackson-databind", "2.9.8", "2.18.2", "2.20.0", OldCritical: 1, OldHigh: 3, OldMedium: 1),
        new("commons-collections:commons-collections", "3.2.1", null, "3.2.2", OldCritical: 1),
        new("org.apache.commons:commons-collections4", null, "4.4", "4.5.0"),
        new("com.google.guava:guava", "20.0", "33.3.1-jre", "33.4.8-jre", OldMedium: 2),
        new("org.hibernate:hibernate-core", "5.2.10.Final", "6.6.4.Final", "7.0.5.Final", OldHigh: 1),
        new("org.apache.commons:commons-lang3", "3.4", "3.17.0", "3.18.0"),
        new("org.apache.httpcomponents:httpclient", "4.5.2", "4.5.14", "4.5.14", OldMedium: 1),
        new("junit:junit", "4.12", "4.13.2", "4.13.2", OldMedium: 1),
    ]);

    private static readonly Ecosystem NuGet = new("NuGet",
    [
        new("Newtonsoft.Json", "9.0.1", "13.0.3", "13.0.3", OldHigh: 1),
        new("log4net", "1.2.10", "3.0.4", "3.1.0", OldCritical: 1),
        new("EntityFramework", "6.1.3", "6.5.1", "6.5.1"),
        new("Microsoft.AspNet.Mvc", "4.0.40804", "5.3.0", "5.3.0", OldHigh: 1),
        new("System.Net.Http", "4.3.0", "4.3.4", "4.3.4", OldHigh: 1),
        new("Serilog", "2.8.0", "4.2.0", "4.3.0"),
        new("Dapper", "1.50.2", "2.1.35", "2.1.66"),
        new("Microsoft.Azure.KeyVault", "3.0.5", null, "3.0.5", DeprecatedOld: true),
        new("Azure.Security.KeyVault.Secrets", null, "4.7.0", "4.8.0"),
    ]);

    private static readonly Ecosystem Npm = new("npm",
    [
        new("request", "2.88.2", null, "2.88.2", DeprecatedOld: true, OldMedium: 2),
        new("moment", "2.24.0", "2.30.1", "2.30.1", OldHigh: 1),
        new("lodash", "4.17.11", "4.17.21", "4.17.21", OldCritical: 1, OldHigh: 2),
        new("express", "4.16.4", "4.21.2", "5.1.0", OldHigh: 1, OldMedium: 1),
        new("axios", "0.19.0", "1.8.4", "1.12.2", OldHigh: 2),
        new("rxjs", "5.5.12", "7.8.1", "7.8.2"),
        new("node-sass", "4.14.1", null, "9.0.0", DeprecatedOld: true, OldHigh: 1, OldMedium: 2),
        new("typescript", "2.9.2", "5.6.3", "5.9.2"),
    ]);

    private static readonly Ecosystem PyPi = new("PyPI",
    [
        new("requests", "2.19.1", "2.32.3", "2.32.5", OldHigh: 1, OldMedium: 1),
        new("urllib3", "1.24.1", "2.2.3", "2.5.0", OldHigh: 2, OldMedium: 1),
        new("flask", "0.12.2", "3.0.3", "3.1.2", OldHigh: 1),
        new("numpy", "1.16.0", "2.1.3", "2.3.2", OldCritical: 1),
        new("pandas", "0.24.2", "2.2.3", "2.3.2"),
        new("pycrypto", "2.6.1", null, "2.6.1", DeprecatedOld: true, OldCritical: 1, OldHigh: 1),
        new("pyyaml", "3.13", "6.0.2", "6.0.2", OldCritical: 1),
    ]);

    private static readonly Ecosystem GoModules = new("Go",
    [
        new("github.com/dgrijalva/jwt-go", "v3.2.0+incompatible", null, "v3.2.0+incompatible", DeprecatedOld: true, OldHigh: 1),
        new("github.com/golang-jwt/jwt/v5", null, "v5.2.1", "v5.3.0"),
        new("github.com/gin-gonic/gin", "v1.6.3", "v1.10.0", "v1.11.0", OldHigh: 1, OldMedium: 1),
        new("golang.org/x/crypto", "v0.0.0-20200622213623-75b288015ac9", "v0.31.0", "v0.42.0", OldCritical: 1, OldHigh: 1),
        new("github.com/sirupsen/logrus", "v1.4.2", "v1.9.3", "v1.9.3"),
        new("gopkg.in/yaml.v2", "v2.2.2", "v2.4.0", "v2.4.0", OldHigh: 1),
    ]);

    private static List<RepositoryDependency> Dependencies(Random random, Era era, Ecosystem ecosystem, int min = 4, int max = 7)
    {
        var oldChance = era switch
        {
            Era.Legacy => 0.8,
            Era.Current => 0.1,
            _ => 0.35,
        };

        var count = Math.Min(ecosystem.Libraries.Length, random.Next(min, max + 1));
        var chosen = ecosystem.Libraries.OrderBy(_ => random.Next()).Take(count);
        var dependencies = new List<RepositoryDependency>();

        foreach (var library in chosen)
        {
            var old = random.NextDouble() < oldChance;
            var version = old ? library.Old ?? library.Current : library.Current ?? library.Old;
            var isOld = version == library.Old && library.Old != library.Current;

            if (version is null)
            {
                continue;
            }

            dependencies.Add(new RepositoryDependency
            {
                Ecosystem = ecosystem.Name,
                Name = library.Name,
                Version = version,
                Latest = library.Latest,
                Direct = random.NextDouble() < 0.85,
                Deprecated = isOld && library.DeprecatedOld,
                Critical = isOld ? library.OldCritical : 0,
                High = isOld ? library.OldHigh : 0,
                Medium = isOld ? library.OldMedium : library.CurrentMedium,
            });
        }

        return dependencies;
    }

    // ------------------------------------------------------------ utilidades

    /// <summary>FNV-1a sobre el código EAI: estable entre procesos, a diferencia de GetHashCode.</summary>
    private static int Seed(string eaiCode)
    {
        unchecked
        {
            var hash = 2166136261u;

            foreach (var b in Encoding.UTF8.GetBytes("code:" + eaiCode))
            {
                hash = (hash ^ b) * 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static string Slug(string text, int max)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var c in text.Trim())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        var slug = builder.ToString().Trim('-');

        if (slug.Length > max)
        {
            slug = slug[..max].TrimEnd('-');
        }

        return slug.Length == 0 ? "app" : slug;
    }
}
