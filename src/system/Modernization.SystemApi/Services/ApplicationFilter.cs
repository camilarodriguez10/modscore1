using Modernization.Domain;

namespace Modernization.SystemApi.Services;

/// <summary>
/// Los criterios con los que se acota el portafolio. Son los mismos en todas las
/// pantallas de consulta desde la Fase 10.
/// </summary>
/// <remarks>
/// Un filtro <b>solo acota</b>. El alcance del usuario recorta primero y este
/// filtro recorta dentro de lo que quedo (ADR 0012): no existe combinacion de
/// parametros que devuelva una aplicacion que el alcance no permita, porque el
/// filtro se aplica sobre un conjunto ya recortado, nunca sobre el original.
/// </remarks>
public sealed record ApplicationFilter
{
    /// <summary>Largo maximo de un filtro. Ningun valor real se acerca.</summary>
    public const int MaxLength = 100;

    public string? Lob { get; init; }
    public string? Country { get; init; }
    public string? Cio { get; init; }
    public string? Disposition { get; init; }
    public string? Roadmap { get; init; }

    /// <summary>Tramo de score: "Critico", "En riesgo", "Aceptable" o "Solido".</summary>
    public string? Band { get; init; }

    public bool? CriticalOnly { get; init; }

    /// <summary>Sin ningun criterio: todo el portafolio dentro del alcance.</summary>
    public static ApplicationFilter None { get; } = new();

    /// <summary>
    /// True si resolverlo exige puntuar. El tramo es el unico criterio que no esta
    /// en los atributos de la aplicacion, y por eso el filtrado vive junto a los
    /// scores en lugar de repartido entre capas.
    /// </summary>
    public bool NeedsScores => !string.IsNullOrWhiteSpace(Band);

    /// <summary>Motivo por el que el filtro es invalido, o null si esta bien formado.</summary>
    public string? Validate()
    {
        foreach (var value in new[] { Lob, Country, Cio, Disposition, Roadmap, Band })
        {
            if (value is { Length: > MaxLength })
            {
                return $"Each filter accepts up to {MaxLength} characters.";
            }
        }

        // Un tramo inexistente es una entrada invalida, no un conjunto vacio:
        // devolver cero resultados haria pensar que el portafolio esta vacio.
        return ScoreBands.IsKnown(Band)
            ? null
            : $"Unknown score band. The bands are: {string.Join(", ", ScoreBands.All.Select(b => b.Code))}.";
    }

    /// <summary>Todo lo que se puede decidir sin puntuar la aplicacion.</summary>
    public bool MatchesAttributes(ApplicationSnapshot application)
    {
        ArgumentNullException.ThrowIfNull(application);

        return Matches(application.Lob, Lob)
            && Matches(application.Country, Country)
            && Matches(application.Cio, Cio)
            && MatchesMaps(
                application.Disposition?.ToString(),
                application.Disposition is { } disposition ? MapsNames.Display(disposition) : null,
                Disposition)
            && MatchesMaps(
                application.Roadmap?.ToString(),
                application.Roadmap is { } roadmap ? MapsNames.Display(roadmap) : null,
                Roadmap)
            && (CriticalOnly != true || application.CriticalApplicationIndicator == true);
    }

    /// <summary>
    /// Disposition y roadmap se aceptan por su nombre de pantalla ("Maintain (Patch)"),
    /// que es el que emiten los agregados y el que se ve en la URL, y tambien por el
    /// nombre del enum ("MaintainPatch") para quien llame al API directamente. Sin lo
    /// primero, hacer clic en una barra de roadmap devolvia un conjunto vacio.
    /// </summary>
    private static bool MatchesMaps(string? key, string? display, string? filter)
        => string.IsNullOrWhiteSpace(filter)
            || string.Equals(key, filter, StringComparison.OrdinalIgnoreCase)
            || string.Equals(display, filter, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(string? value, string? filter)
        => string.IsNullOrWhiteSpace(filter)
            || string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
}
