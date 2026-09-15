using System.Reflection;

namespace Modernization.Scoring.Engine;

/// <summary>
/// Rulesets publicados. Se embeben en el ensamblado para que el motor no dependa
/// del layout del disco y para que la version quede atada al binario desplegado.
/// </summary>
public static class RulesetCatalog
{
    /// <summary>Version por defecto: la que reproduce el dashboard actual.</summary>
    public const string DefaultVersion = "v1.3";

    private static readonly Lazy<Ruleset> DefaultRuleset = new(() => Load(DefaultVersion));

    /// <summary>Ruleset v1.3, cargado y validado una sola vez.</summary>
    public static Ruleset Default => DefaultRuleset.Value;

    /// <summary>Carga un ruleset embebido por version.</summary>
    /// <exception cref="RulesetException">La version no existe.</exception>
    public static Ruleset Load(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var resourceName = $"ruleset-{version}.json";
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new RulesetException(
                $"No existe el ruleset '{version}'. Disponibles: " +
                $"{string.Join(", ", assembly.GetManifestResourceNames())}.");

        using var reader = new StreamReader(stream);
        return Ruleset.FromJson(reader.ReadToEnd());
    }
}
