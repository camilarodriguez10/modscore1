using System.Globalization;

namespace Modernization.Domain;

/// <summary>
/// Tramos de score. Son una convencion de lectura, no una regla del BRD: el
/// modelo normaliza a 20-100, asi que el piso del tramo mas bajo es 20.
/// </summary>
/// <remarks>
/// Viven junto al dominio y no en la capa de presentacion porque desde la Fase 10
/// el tramo tambien es un <b>filtro</b>: pedir "solo los criticos" recorta el
/// conjunto antes de paginar, y eso solo puede hacerlo quien tiene los scores.
/// <para>
/// Desde la Fase 11 cada tramo es un <b>codigo</b> —<c>critical</c>, <c>at-risk</c>—
/// y la interfaz le pone nombre (ADR 0017). Los nombres en espanol de la Fase 10 se
/// siguen aceptando como filtro, para que un enlace compartido entonces no se rompa.
/// </para>
/// </remarks>
public static class ScoreBands
{
    /// <summary>Los cuatro tramos, del peor al mejor.</summary>
    public static readonly IReadOnlyList<ScoreBand> All =
    [
        new("critical", 20m, 49.99m, "Critico"),
        new("at-risk", 50m, 69.99m, "En riesgo"),
        new("acceptable", 70m, 84.99m, "Aceptable"),
        new("solid", 85m, 100m, "Solido"),
    ];

    /// <summary>Codigo del tramo al que pertenece un score.</summary>
    public static string Of(decimal score)
    {
        foreach (var band in All)
        {
            if (score >= band.Min && score <= band.Max)
            {
                return band.Code;
            }
        }

        return All[0].Code;
    }

    /// <summary>
    /// Resuelve un tramo tal como llega en la URL: por su codigo o por el nombre que
    /// tuvo en la Fase 10. Devuelve null si no existe: un filtro desconocido es una
    /// entrada invalida, no un conjunto vacio.
    /// </summary>
    public static ScoreBand? Find(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return All.FirstOrDefault(b =>
            string.Equals(b.Code, trimmed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(b.LegacyName, trimmed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True si el valor corresponde a un tramo, o si no hay filtro.</summary>
    public static bool IsKnown(string? value) => string.IsNullOrWhiteSpace(value) || Find(value) is not null;

    /// <summary>True si el score cae en el tramo pedido. Sin filtro, todo pasa.</summary>
    public static bool Matches(decimal score, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var band = Find(value);

        return band is not null && score >= band.Min && score <= band.Max;
    }
}

/// <summary>Un tramo con sus limites, ambos inclusivos.</summary>
/// <param name="Code">Codigo estable; la interfaz le pone nombre.</param>
/// <param name="Min">Limite inferior, inclusivo.</param>
/// <param name="Max">Limite superior, inclusivo.</param>
/// <param name="LegacyName">Nombre que tuvo en la Fase 10, aceptado como filtro.</param>
public sealed record ScoreBand(string Code, decimal Min, decimal Max, string? LegacyName = null)
{
    /// <summary>El rango como dato: "70-85".</summary>
    public string Range => string.Create(CultureInfo.InvariantCulture, $"{Min:0}-{Max:0}");
}
