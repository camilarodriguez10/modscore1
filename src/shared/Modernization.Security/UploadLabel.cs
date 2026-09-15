using System.Globalization;

namespace Modernization.Security;

/// <summary>
/// La etiqueta de una carga: texto libre que se guarda en el manifiesto, aparece en
/// pantalla y queda en la auditoria. Se valida igual en las dos capas.
/// </summary>
public static class UploadLabel
{
    public const int MaxLength = 80;

    /// <summary>
    /// Normaliza la etiqueta. Una vacia es valida y queda en null: el sistema pone
    /// una por defecto.
    /// </summary>
    /// <remarks>
    /// Se rechazan los caracteres de control, que permitirian fabricar lineas en un
    /// log de texto, y los de formato (U+202E y compania), que invierten el orden
    /// visual y hacen que una etiqueta se lea en pantalla distinto de lo que es.
    /// <para>
    /// El error va en ingles: es el <c>detail</c> del 400 que devuelve el borde.
    /// </para>
    /// </remarks>
    public static bool TryNormalize(string? raw, out string? label, out string? error)
    {
        label = null;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var trimmed = raw.Trim();

        if (trimmed.Length > MaxLength)
        {
            error = string.Create(CultureInfo.InvariantCulture, $"The label can be up to {MaxLength} characters long.");
            return false;
        }

        foreach (var character in trimmed)
        {
            if (char.IsControl(character)
                || char.GetUnicodeCategory(character) == UnicodeCategory.Format)
            {
                error = "The label cannot contain line breaks, control characters or formatting characters.";
                return false;
            }
        }

        label = trimmed;
        return true;
    }
}
