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
        => await PublishAsync(files, label, uploadedBy, assignments, null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Publica una carga con su mapping de columnas. <paramref name="columnMaps"/> lleva,
    /// en el mismo orden que los archivos, un objeto JSON de campo a columna del archivo
    /// (o a null si no la trae); vacío o "-" deja que lo decidan el nombre y los alias.
    /// </summary>
    public async Task<PublishOutcome> PublishAsync(
        IFormFileCollection files,
        string? label,
        string uploadedBy,
        IReadOnlyList<string>? assignments,
        IReadOnlyList<string>? columnMaps,
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
        if ((assignments is { Count: > 0 } && assignments.Count != files.Count)
            || (columnMaps is { Count: > 0 } && columnMaps.Count != files.Count))
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

            if (!TryParseColumnMap(columnMaps, index, out var columnMap))
            {
                rejected.Add(Notice.Of(NoticeCodes.MappingInvalid, ("file", name)));
                continue;
            }

            incoming.Add(new IncomingSourceFile(name, file.Length, file.OpenReadStream)
            {
                AssignedFileName = assigned,
                ColumnMap = columnMap,
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

    /// <summary>
    /// Revisa el mapping antes de cargar: identifica la fuente de cada archivo por su
    /// encabezado y propone qué columna carga cada campo, con lo recordado de la carga
    /// vigente y las decisiones que ya llegaron. No publica ni guarda nada. Las métricas
    /// del ruleset vigente, con su campo de origen, dicen qué alimenta cada campo.
    /// </summary>
    public async Task<MappingPreviewResponse> PreviewMappingAsync(
        MappingPreviewRequest request,
        IReadOnlyList<(string Name, string Field)> metrics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(metrics);

        var remembered = publisher is null
            ? []
            : await publisher.GetCurrentColumnMapsAsync(cancellationToken).ConfigureAwait(false);

        var matcher = new SourceFileMatcher(catalog);
        var files = new List<MappingPreviewFileResponse>();

        foreach (var file in request.Files ?? [])
        {
            var name = Path.GetFileName(file.FileName ?? string.Empty);
            name = name.Length > MaxPreviewFileNameLength ? name[..MaxPreviewFileNameLength] : name;
            var assigned = file.AssignedFileName?.Trim() is { Length: > 0 } value && value != "-" ? value : null;

            IReadOnlyList<string> headers;

            try
            {
                headers = ColumnMapping.ParseHeaderLine(file.Header);
            }
            catch (SourceSchemaException ex)
            {
                files.Add(Unresolved(name, [], catalog.ExpectedFileNames, ex.Notice ?? Notice.Of(NoticeCodes.CsvNoHeader)));
                continue;
            }

            if (assigned is not null && !IsValidAssignment(assigned))
            {
                files.Add(Unresolved(name, headers, catalog.ExpectedFileNames, Notice.Of(NoticeCodes.FileAssignmentShape)));
                continue;
            }

            var match = matcher.Match(headers, name, assigned);

            if (match.Schema is null)
            {
                files.Add(Unresolved(name, headers, match.Candidates, match.Problem));
                continue;
            }

            var schema = match.Schema;
            var resolution = ColumnMapping.Resolve(
                schema,
                headers,
                file.Columns,
                remembered.FirstOrDefault(m => string.Equals(m.FileName, schema.FileName, StringComparison.OrdinalIgnoreCase)));

            files.Add(new MappingPreviewFileResponse(
                name,
                schema.FileName,
                schema.Source,
                schema.Description,
                null,
                [],
                headers,
                [.. resolution.Columns.Select(c => new MappingFieldResponse(
                    c.Column.Name,
                    TypeName(c.Column.Type),
                    c.Column.Required,
                    string.Equals(c.Column.Name, schema.KeyColumn, StringComparison.Ordinal),
                    c.Header,
                    MatchName(c.How),
                    [.. metrics.Where(m => MetricFeeds.Uses(m.Field, c.Column.Name)).Select(m => m.Name).Distinct(StringComparer.Ordinal)]))],
                resolution.UnusedHeaders,
                resolution.Problems));
        }

        return new MappingPreviewResponse(files);
    }

    /// <summary>
    /// De dónde sale cada campo de la base, sin archivos: el contrato de cada fuente, los
    /// nombres que se reconocen solos y la columna registrada en la carga vigente.
    /// </summary>
    public async Task<MappingCatalogResponse> DescribeMappingAsync(
        IReadOnlyList<(string Name, string Field)> metrics,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metrics);

        var current = publisher is null
            ? []
            : await publisher.GetCurrentColumnMapsAsync(cancellationToken).ConfigureAwait(false);

        var sources = catalog.Files.Select(schema =>
        {
            var map = current.FirstOrDefault(m => string.Equals(m.FileName, schema.FileName, StringComparison.OrdinalIgnoreCase));

            return new MappingSourceResponse(
                schema.Source,
                schema.FileName,
                schema.Description,
                schema.KeyColumn,
                [.. schema.Columns.Select(column =>
                {
                    var assignment = map?.Columns.FirstOrDefault(a => string.Equals(a.Column, column.Name, StringComparison.Ordinal));

                    return new MappingCatalogFieldResponse(
                        column.Name,
                        TypeName(column.Type),
                        column.Required,
                        string.Equals(column.Name, schema.KeyColumn, StringComparison.Ordinal),
                        [column.Name, .. column.Aliases],
                        assignment?.Header,
                        assignment?.Manual,
                        [.. metrics.Where(m => MetricFeeds.Uses(m.Field, column.Name)).Select(m => m.Name).Distinct(StringComparer.Ordinal)]);
                })]);
        }).ToList();

        return new MappingCatalogResponse(current.Count > 0, sources);
    }

    /// <summary>Tope del nombre de archivo que se devuelve en la revisión.</summary>
    private const int MaxPreviewFileNameLength = 256;

    /// <summary>Tope del JSON de mapping de un archivo.</summary>
    public const int MaxColumnMapLength = 32 * 1024;

    /// <summary>Tope de campos en el mapping de un archivo. Los contratos tienen menos de 30.</summary>
    public const int MaxColumnMapEntries = 200;

    private static MappingPreviewFileResponse Unresolved(
        string name,
        IReadOnlyList<string> headers,
        IReadOnlyList<string> candidates,
        Notice? problem)
        => new(name, null, "-", null, problem, candidates, headers, [], headers, []);

    private static string TypeName(ColumnType type) => type switch
    {
        ColumnType.Int => "integer",
        ColumnType.Long => "long",
        ColumnType.Decimal => "number",
        ColumnType.Bool => "boolean",
        ColumnType.DateTime => "date",
        _ => "text",
    };

    private static string MatchName(ColumnMatch how) => how switch
    {
        ColumnMatch.Name => "name",
        ColumnMatch.Alias => "alias",
        ColumnMatch.Manual => "manual",
        ColumnMatch.Remembered => "remembered",
        ColumnMatch.Excluded => "excluded",
        _ => "missing",
    };

    /// <summary>
    /// El mapping de un archivo, validado por forma: un objeto de campo a columna o a null,
    /// acotado y sin caracteres de control. Que los campos existan y las columnas estén en
    /// el archivo lo comprueba el publicador contra el contrato.
    /// </summary>
    private static bool TryParseColumnMap(IReadOnlyList<string>? columnMaps, int index, out IReadOnlyDictionary<string, string?>? map)
    {
        map = null;

        if (columnMaps is null || index >= columnMaps.Count)
        {
            return true;
        }

        var raw = columnMaps[index]?.Trim();

        if (string.IsNullOrEmpty(raw) || raw == "-")
        {
            return true;
        }

        if (raw.Length > MaxColumnMapLength)
        {
            return false;
        }

        Dictionary<string, string?>? parsed;

        try
        {
            parsed = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(raw);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }

        if (parsed is null
            || parsed.Count > MaxColumnMapEntries
            || parsed.Any(entry => !IsCleanText(entry.Key, 128) || (entry.Value is not null && !IsCleanText(entry.Value, ColumnMapping.MaxHeaderNameLength))))
        {
            return false;
        }

        map = parsed;
        return true;
    }

    private static bool IsCleanText(string text, int maxLength)
        => text.Length is > 0 && text.Length <= maxLength && !text.Any(char.IsControl);

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
