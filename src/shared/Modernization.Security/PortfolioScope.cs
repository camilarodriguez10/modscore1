using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Modernization.Security;

/// <summary>
/// Que parte del portafolio puede ver quien hace la peticion.
/// </summary>
/// <remarks>
/// El filtrado ocurre en la capa System, sobre los datos, no en la interfaz (plan
/// secc. 7.2). La capa Channel traduce los claims del usuario a este valor y lo
/// reenvia en un header que la capa System solo acepta de un llamador autenticado
/// con token de servicio: el modelo de subsistema de confianza (ADR 0012).
/// <para>
/// Por defecto no se ve nada. Un usuario sin claim de alcance, o una peticion sin
/// header, recibe un portafolio vacio — nunca el completo.
/// </para>
/// </remarks>
public sealed class PortfolioScope
{
    public const string HeaderName = "X-Portfolio-Scope";

    /// <summary>Entrada que concede el portafolio completo.</summary>
    public const string FullValue = "*";

    private const int MaxValueLength = 100;

    /// <summary>Tope de entradas: un header con miles no es un alcance, es un ataque.</summary>
    private const int MaxEntries = 64;

    private readonly HashSet<string> _lobs;
    private readonly HashSet<string> _countries;
    private readonly HashSet<string> _cios;

    private PortfolioScope(bool isFull, HashSet<string> lobs, HashSet<string> countries, HashSet<string> cios)
    {
        IsFull = isFull;
        _lobs = lobs;
        _countries = countries;
        _cios = cios;
    }

    public static PortfolioScope Full { get; } = new(true, NewSet(), NewSet(), NewSet());

    public static PortfolioScope None { get; } = new(false, NewSet(), NewSet(), NewSet());

    public bool IsFull { get; }

    /// <summary>True cuando no concede ninguna aplicacion.</summary>
    public bool IsEmpty => !IsFull && _lobs.Count == 0 && _countries.Count == 0 && _cios.Count == 0;

    /// <summary>
    /// Una aplicacion es visible si coincide con cualquiera de las entradas: el
    /// alcance es la union de lo concedido, no la interseccion.
    /// </summary>
    public bool Allows(string? lob, string? country, string? cio)
        => IsFull
            || (lob is not null && _lobs.Contains(lob))
            || (country is not null && _countries.Contains(country))
            || (cio is not null && _cios.Contains(cio));

    /// <summary>
    /// Lee el alcance de los claims del usuario. Cada claim es "*", "lob:Valor",
    /// "country:Valor" o "cio:Valor". Una entrada que no cumple el formato se
    /// ignora: nunca amplia el alcance.
    /// </summary>
    public static PortfolioScope FromClaims(ClaimsPrincipal user, string claimType)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimType);

        return Parse(user.FindAll(claimType).Select(c => c.Value), escaped: false);
    }

    public static PortfolioScope FromHeader(string? header)
        => string.IsNullOrEmpty(header) ? None : Parse(header.Split(';'), escaped: true);

    /// <summary>
    /// Forma de header: entradas separadas por ';' con el valor escapado, para que
    /// un LOB como "Retirement &amp; Income Solutions" viaje intacto y en ASCII.
    /// </summary>
    public string ToHeaderValue()
    {
        if (IsFull)
        {
            return FullValue;
        }

        return string.Join(
            ';',
            Entries("lob", _lobs).Concat(Entries("country", _countries)).Concat(Entries("cio", _cios)));

        static IEnumerable<string> Entries(string dimension, HashSet<string> values)
            => values
                .Order(StringComparer.Ordinal)
                .Select(v => string.Create(CultureInfo.InvariantCulture, $"{dimension}:{Uri.EscapeDataString(v)}"));
    }

    /// <summary>
    /// Enlace de parametros de minimal APIs: toda operacion de la capa System que
    /// lee el portafolio declara el alcance como parametro, asi que olvidarlo no
    /// compila. Solo un llamador autenticado como servicio puede declararlo; para
    /// cualquier otro el alcance es vacio.
    /// </summary>
    public static ValueTask<PortfolioScope?> BindAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ValueTask.FromResult<PortfolioScope?>(
            ServiceCaller.Is(context.User)
                ? FromHeader(context.Request.Headers[HeaderName].ToString())
                : None);
    }

    private static PortfolioScope Parse(IEnumerable<string> entries, bool escaped)
    {
        var lobs = NewSet();
        var countries = NewSet();
        var cios = NewSet();
        var count = 0;

        foreach (var raw in entries)
        {
            if (++count > MaxEntries)
            {
                break;
            }

            var entry = raw.Trim();

            if (entry == FullValue)
            {
                return Full;
            }

            var separator = entry.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var value = entry[(separator + 1)..];
            if (escaped)
            {
                value = Uri.UnescapeDataString(value);
            }

            value = value.Trim();
            if (!IsAcceptable(value))
            {
                continue;
            }

            var target = entry[..separator].ToUpperInvariant() switch
            {
                "LOB" => lobs,
                "COUNTRY" => countries,
                "CIO" => cios,
                _ => null,
            };

            target?.Add(value);
        }

        return new PortfolioScope(false, lobs, countries, cios);
    }

    private static bool IsAcceptable(string value)
        => value.Length is > 0 and <= MaxValueLength && !value.Any(char.IsControl);

    private static HashSet<string> NewSet() => new(StringComparer.OrdinalIgnoreCase);
}
