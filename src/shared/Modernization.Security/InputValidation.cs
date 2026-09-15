using System.Buffers;

namespace Modernization.Security;

/// <summary>
/// Validacion de entrada por lista blanca, aplicada en el borde y en cada capa.
/// </summary>
/// <remarks>
/// Un identificador que no cumple el formato no llega al compositor, ni a la URL
/// de la capa System, ni de vuelta al cliente dentro de un mensaje de error. Es
/// mas barato rechazarlo en la puerta que sanearlo en cada salida.
/// </remarks>
public static class InputValidation
{
    /// <summary>Longitud maxima de un EAI code observada en las fuentes reales.</summary>
    private const int MaxEaiCodeLength = 10;

    private static readonly SearchValues<char> Digits = SearchValues.Create("0123456789");

    /// <summary>
    /// Un EAI code es una secuencia corta de digitos en todas las fuentes
    /// observadas. Cualquier otra cosa se rechaza sin procesarse.
    /// </summary>
    public static bool IsValidEaiCode(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxEaiCodeLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Digits.Contains(character))
            {
                return false;
            }
        }

        return true;
    }
}
