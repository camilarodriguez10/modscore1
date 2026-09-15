using System.Text.Json;
using Modernization.Domain;

namespace Modernization.Sources.Database;

/// <summary>
/// El motivo de un rechazo se guarda como el JSON del aviso: codigo y datos.
/// </summary>
/// <remarks>
/// Sin columna nueva ni migracion: <c>source_rejection.Reason</c> ya es texto sin
/// limite, y la tabla es metadato del sistema, no una fuente (ADR 0014). Las filas
/// guardadas antes de la Fase 11 traen una frase en espanol en lugar de JSON; se
/// leen como "fila no convertible" en vez de romper la pantalla de linaje.
/// </remarks>
internal static class NoticeStorage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Serialize(Notice notice)
        => JsonSerializer.Serialize(new Stored(notice.Code, new Dictionary<string, string>(notice.Args)), Json);

    public static Notice Deserialize(string stored)
    {
        if (!string.IsNullOrWhiteSpace(stored) && stored.TrimStart().StartsWith('{'))
        {
            try
            {
                var value = JsonSerializer.Deserialize<Stored>(stored, Json);

                if (value is { Code.Length: > 0 })
                {
                    return new Notice(value.Code, value.Args);
                }
            }
            catch (JsonException)
            {
                // Cae al aviso generico de abajo.
            }
        }

        return Notice.Of(NoticeCodes.RowNotConvertible);
    }

    private sealed record Stored(string Code, Dictionary<string, string>? Args);
}
