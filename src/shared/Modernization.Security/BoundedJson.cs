using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Modernization.Security;

/// <summary>El cuerpo leido, o el problema que hay que responder en su lugar.</summary>
public sealed record BoundedJsonResult<T>(T? Value, IResult? Problem)
    where T : class;

/// <summary>
/// Lee un cuerpo JSON con un tope propio y un deserializador estricto.
/// </summary>
/// <remarks>
/// El enlace automatico de minimal APIs lee con el limite general de 1 MB y acepta
/// propiedades desconocidas. Para un borrador de reglas eso sobra en ambos sentidos:
/// el cuerpo cabe en pocos KB, y un campo que no existe en el contrato es un cliente
/// equivocado o alguien probando, no algo que ignorar en silencio.
/// </remarks>
public static class BoundedJson
{
    private const int ChunkBytes = 16 * 1024;

    /// <summary>
    /// Sin propiedades desconocidas, sin nulos donde el contrato no los admite, sin
    /// parametros obligatorios ausentes y con profundidad acotada.
    /// </summary>
    public static JsonSerializerOptions StrictOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        MaxDepth = 16,
    };

    public static async Task<BoundedJsonResult<T>> ReadAsync<T>(
        HttpRequest request,
        long maxBytes,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.HasJsonContentType())
        {
            return Fail<T>(ApiProblem.Create(
                StatusCodes.Status415UnsupportedMediaType,
                "unsupported-media-type",
                "Invalid request format",
                "Send the body as application/json."));
        }

        if (request.ContentLength > maxBytes)
        {
            return TooLarge<T>();
        }

        // El servidor corta antes de leer de mas; el conteo de abajo cubre los hosts
        // sin esa caracteristica y los cuerpos sin Content-Length.
        var limit = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (limit is { IsReadOnly: false })
        {
            limit.MaxRequestBodySize = maxBytes;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[ChunkBytes];

        try
        {
            int read;

            while ((read = await request.Body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (buffer.Length + read > maxBytes)
                {
                    return TooLarge<T>();
                }

                buffer.Write(chunk, 0, read);
            }
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge<T>();
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(buffer.GetBuffer().AsSpan(0, (int)buffer.Length), StrictOptions);

            return value is null ? Invalid<T>() : new BoundedJsonResult<T>(value, null);
        }
        catch (JsonException)
        {
            // Sin eco del error del parser: dice en que propiedad fallo y con que
            // valor, y eso no le sirve a nadie mas que a quien esta probando.
            return Invalid<T>();
        }
    }

    private static BoundedJsonResult<T> Fail<T>(IResult problem) where T : class => new(null, problem);

    private static BoundedJsonResult<T> TooLarge<T>() where T : class => Fail<T>(ApiProblem.Create(
        StatusCodes.Status413PayloadTooLarge,
        "body-too-large",
        "Request too large",
        "The request body exceeds the accepted size."));

    private static BoundedJsonResult<T> Invalid<T>() where T : class => Fail<T>(ApiProblem.Create(
        StatusCodes.Status400BadRequest,
        "invalid-body",
        "Invalid request body",
        "The body is not valid JSON for this operation, or it has missing or unknown fields."));
}
