using Microsoft.AspNetCore.Http;
using Modernization.Domain.Sources;
using Modernization.Security;
using Modernization.Sources.Files;
using Modernization.SystemApi.Contracts;
using Modernization.Domain;

namespace Modernization.SystemApi.Services;

/// <summary>
/// Adapta la carga HTTP al servicio de archivos. Aqui viven los controles que
/// dependen del transporte; los que dependen del contenido viven en
/// <see cref="UploadService"/>.
/// </summary>
public sealed class IngestionService(
    ISnapshotPublisher? publisher,
    SourceSchemaCatalog catalog,
    TimeProvider clock)
{
    /// <summary>Tope de archivos por carga. Son ocho fuentes; mas es un error.</summary>
    public const int MaxFiles = 12;

    /// <summary>Extension aceptada. Los exports observados son CSV.</summary>
    private const string AllowedExtension = ".csv";

    private static readonly string[] AllowedContentTypes =
    [
        "text/csv",
        "application/csv",
        "text/plain",
        "application/vnd.ms-excel",
        "application/octet-stream",
    ];

    /// <summary>True cuando el adaptador activo admite cargas.</summary>
    public bool IsEnabled => publisher is not null;

    public IReadOnlyList<ExpectedFileResponse> DescribeExpectedFiles()
        => [.. catalog.Files.Select(f => new ExpectedFileResponse(
            f.Source,
            f.FileName,
            f.Description,
            f.KeyColumn,
            [.. f.Columns.Where(c => c.Required).Select(c => c.Name)],
            [.. f.Columns.Where(c => !c.Required).Select(c => c.Name)]))];

    /// <summary>
    /// Publica una carga. <paramref name="assignments"/> lleva, en el mismo orden
    /// que los archivos, la fuente que el usuario asigno a mano cuando el encabezado
    /// no bastaba; vacio o "-" significa que lo decida el encabezado.
    /// </summary>
    public async Task<PublishOutcome> PublishAsync(
        IFormFileCollection files,
        string? label,
        string uploadedBy,
        IReadOnlyList<string>? assignments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        // La validacion de entrada va primero, siempre. Que la funcion este
        // deshabilitada no es razon para dejar pasar sin revisar lo que llega:
        // el orden inverso haria que un archivo malformado se rechazara por el
        // motivo equivocado, y que la validacion nunca se ejercitara.
        if (files.Count > MaxFiles)
        {
            return new PublishOutcome(
                false,
                null,
                [],
                [Notice.Of(NoticeCodes.UploadTooManyFiles, ("max", MaxFiles), ("count", files.Count))],
                0);
        }

        // La etiqueta se guarda en el manifiesto, se muestra y queda en la
        // auditoria: se valida igual que en la capa Channel.
        if (!UploadLabel.TryNormalize(label, out var normalizedLabel, out _))
        {
            return new PublishOutcome(
                false,
                null,
                [],
                [Notice.Of(NoticeCodes.UploadLabelInvalid, ("max", UploadLabel.MaxLength))],
                0);
        }

        // Si vienen asignaciones, tienen que corresponder una a una con los archivos:
        // una lista desalineada asignaria el contenido de un archivo a la fuente de
        // otro, que es peor que no asignar nada.
        if (assignments is { Count: > 0 } && assignments.Count != files.Count)
        {
            return new PublishOutcome(
                false,
                null,
                [],
                [Notice.Of(NoticeCodes.UploadAssignmentMisaligned)],
                0);
        }

        var rejected = new List<Notice>();
        var incoming = new List<IncomingSourceFile>(files.Count);

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];

            // Solo el nombre, nunca la ruta: un cliente puede enviar
            // "../../appsettings.json" como FileName y hay que descartarlo antes
            // de que toque nada.
            var name = Path.GetFileName(file.FileName);

            if (string.IsNullOrWhiteSpace(name))
            {
                rejected.Add(Notice.Of(NoticeCodes.FileNoName));
                continue;
            }

            if (!name.EndsWith(AllowedExtension, StringComparison.OrdinalIgnoreCase))
            {
                rejected.Add(Notice.Of(NoticeCodes.FileExtension, ("extension", AllowedExtension), ("file", name)));
                continue;
            }

            if (!AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                rejected.Add(Notice.Of(NoticeCodes.FileContentType, ("contentType", file.ContentType), ("file", name)));
                continue;
            }

            var assigned = Assignment(assignments, index);

            if (assigned is { } value && !IsValidAssignment(value))
            {
                rejected.Add(Notice.Of(NoticeCodes.FileAssignmentShape, ("file", name)));
                continue;
            }

            incoming.Add(new IncomingSourceFile(name, file.Length, file.OpenReadStream)
            {
                AssignedFileName = assigned,
            });
        }

        if (rejected.Count > 0)
        {
            return new PublishOutcome(false, null, [], rejected, 0);
        }

        if (publisher is null)
        {
            return new PublishOutcome(
                false,
                null,
                [],
                [Notice.Of(NoticeCodes.UploadNotSupported)],
                0);
        }

        var now = clock.GetUtcNow();

        return await publisher.PublishAsync(
            incoming,
            normalizedLabel ?? FormattableString.Invariant($"Upload {now:yyyy-MM-dd HH:mm}"),
            uploadedBy,
            now,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>La asignacion de un archivo, o null si se deja al encabezado.</summary>
    private static string? Assignment(IReadOnlyList<string>? assignments, int index)
    {
        if (assignments is null || index >= assignments.Count)
        {
            return null;
        }

        var value = assignments[index]?.Trim();

        return string.IsNullOrEmpty(value) || value == "-" ? null : value;
    }

    /// <summary>
    /// Forma de nombre de archivo, por lista blanca. Que exista en el catalogo lo
    /// comprueba el emparejador; esto solo evita que llegue alli una cadena con
    /// separadores de ruta o caracteres de control.
    /// </summary>
    private static bool IsValidAssignment(string value)
    {
        if (value.Length > 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
