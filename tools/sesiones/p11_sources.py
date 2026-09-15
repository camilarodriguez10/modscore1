from p11_lib import patch, ensure_using

# ------------------------------------------------------------ SourceSchema.cs
patch("src/system/Modernization.Sources/Files/SourceSchema.cs", [
    ("""public readonly record struct ParseOutcome(bool Ok, object? Value, string? Error)
{
    public static ParseOutcome Empty => new(true, null, null);

    public static ParseOutcome Success(object value) => new(true, value, null);

    public static ParseOutcome Failure(string error) => new(false, null, error);
}""",
     """public readonly record struct ParseOutcome(bool Ok, object? Value, Notice? Error)
{
    public static ParseOutcome Empty => new(true, null, null);

    public static ParseOutcome Success(object value) => new(true, value, null);

    public static ParseOutcome Failure(Notice error) => new(false, null, error);
}"""),
    ("""public static class ColumnParser
{
""",
     """public static class ColumnParser
{
    /// <summary>Cuanto del valor original se devuelve en un rechazo.</summary>
    private const int MaxEchoedValue = 64;

"""),
    ("""            _ => ParseOutcome.Failure($"Tipo de columna desconocido: {column.Type}."),""",
     """            _ => ParseOutcome.Failure(Invalid(column, "unknown", text)),"""),
    ("""            : ParseOutcome.Failure($"'{column.Name}' esperaba un entero y recibio '{text}'.");""",
     """            : ParseOutcome.Failure(Invalid(column, "integer", text));"""),
    ("""            : ParseOutcome.Failure($"'{column.Name}' esperaba un entero largo y recibio '{text}'.");""",
     """            : ParseOutcome.Failure(Invalid(column, "long", text));"""),
    ("""            : ParseOutcome.Failure($"'{column.Name}' esperaba un numero y recibio '{text}'.");""",
     """            : ParseOutcome.Failure(Invalid(column, "number", text));"""),
    ("""        _ => ParseOutcome.Failure($"'{column.Name}' esperaba TRUE o FALSE y recibio '{text}'."),""",
     """        _ => ParseOutcome.Failure(Invalid(column, "boolean", text)),"""),
    ("""            : ParseOutcome.Failure($"'{column.Name}' esperaba una fecha y recibio '{text}'.");""",
     """            : ParseOutcome.Failure(Invalid(column, "date", text));"""),
    ("""    /// <summary>Quita espacios no separables y simbolos de porcentaje que dejan los exports.</summary>""",
     """    /// <summary>
    /// Un valor que no se pudo convertir, como codigo y datos (ADR 0017). El valor se
    /// acota: es contenido del archivo y termina en la pantalla de linaje.
    /// </summary>
    private static Notice Invalid(ColumnSchema column, string expected, string text)
        => Notice.Of(
            NoticeCodes.RowInvalidValue,
            ("column", column.Name),
            ("expected", expected),
            ("value", text.Length > MaxEchoedValue ? text[..MaxEchoedValue] + "…" : text));

    /// <summary>Quita espacios no separables y simbolos de porcentaje que dejan los exports.</summary>"""),
    ("""/// <summary>Contrato de esquema invalido, o archivo que no lo cumple.</summary>
public sealed class SourceSchemaException : Exception
{
    public SourceSchemaException(string message) : base(message) { }""",
     """/// <summary>Contrato de esquema invalido, o archivo que no lo cumple.</summary>
/// <remarks>
/// Dos usos distintos. Un archivo que no cumple su contrato lleva un <see cref="Notice"/>
/// que termina en la pantalla de cargas (ADR 0017). Un contrato mal escrito falla al
/// arrancar y su mensaje es para quien opera, asi que sigue siendo texto.
/// </remarks>
public sealed class SourceSchemaException : Exception
{
    public SourceSchemaException(Notice notice) : base(notice?.ToString()) => Notice = notice;

    public SourceSchemaException(Notice notice, Exception innerException)
        : base(notice?.ToString(), innerException) => Notice = notice;

    public SourceSchemaException(string message) : base(message) { }"""),
    ("""    public SourceSchemaException() { }
}""",
     """    public SourceSchemaException() { }

    /// <summary>Que no cumple el archivo, cuando el problema es del archivo y no del contrato.</summary>
    public Notice? Notice { get; }
}"""),
])
ensure_using("src/system/Modernization.Sources/Files/SourceSchema.cs", "Modernization.Domain")

# --------------------------------------------------------- CsvSourceReader.cs
patch("src/system/Modernization.Sources/Files/CsvSourceReader.cs", [
    ("""    public const int MaxFieldLength = 4096;
""",
     """    public const int MaxFieldLength = 4096;

    /// <summary>Cuantas columnas del encabezado se devuelven cuando falta alguna obligatoria.</summary>
    private const int MaxEchoedHeaders = 40;

    /// <summary>Largo maximo de cada columna devuelta.</summary>
    private const int MaxEchoedHeaderLength = 64;
"""),
    ("""        catch (MaxFieldSizeException ex)
        {
            throw new SourceSchemaException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{_schema.FileName}' tiene un campo de mas de {MaxFieldLength:N0} caracteres (fila {ex.Context?.Parser?.Row}). ")
                + "Ninguna columna de las fuentes necesita tanto: el archivo esta corrupto o no es el export esperado.",
                ex);
        }
        catch (CsvHelperException ex)
        {
            // Sin el mensaje de CsvHelper: incluye el contenido crudo de la fila.
            throw new SourceSchemaException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"'{_schema.FileName}' no se pudo leer como CSV (fila {ex.Context?.Parser?.Row})."),
                ex);
        }""",
     """        catch (MaxFieldSizeException ex)
        {
            // Ninguna columna de las fuentes necesita tanto: el archivo esta corrupto
            // o no es el export esperado.
            throw new SourceSchemaException(
                Notice.Of(NoticeCodes.CsvFieldTooLong, ("max", MaxFieldLength), ("row", ex.Context?.Parser?.Row)),
                ex);
        }
        catch (CsvHelperException ex)
        {
            // Sin el mensaje de CsvHelper: incluye el contenido crudo de la fila.
            throw new SourceSchemaException(
                Notice.Of(NoticeCodes.CsvUnreadable, ("row", ex.Context?.Parser?.Row)),
                ex);
        }"""),
    ("""                throw new SourceSchemaException("el archivo no tiene encabezado.");""",
     """                throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvNoHeader));"""),
    ("""            throw new SourceSchemaException(
                $"su encabezado tiene un campo de mas de {MaxFieldLength:N0} caracteres: no es un CSV de fuente.",
                ex);""",
     """            throw new SourceSchemaException(
                Notice.Of(NoticeCodes.CsvFieldTooLong, ("max", MaxFieldLength), ("row", 1)),
                ex);"""),
    ("""            throw new SourceSchemaException("su encabezado no se pudo leer como CSV.", ex);""",
     """            throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)), ex);"""),
    ("""            throw new SourceSchemaException($"El archivo '{_schema.FileName}' no tiene encabezado.");
        }

        var headers = csv.HeaderRecord
            ?? throw new SourceSchemaException($"El archivo '{_schema.FileName}' no tiene encabezado.");""",
     """            throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvNoHeader));
        }

        var headers = csv.HeaderRecord
            ?? throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvNoHeader));"""),
    ("""                throw new SourceSchemaException(
                    $"'{_schema.FileName}' supera el limite de {MaxRows:N0} filas.");""",
     """                throw new SourceSchemaException(Notice.Of(NoticeCodes.CsvTooManyRows, ("max", MaxRows)));"""),
    ("""            throw new SourceSchemaException(
                $"A '{_schema.FileName}' le faltan columnas obligatorias: {string.Join(", ", missing)}. "
                + $"El archivo trae: {string.Join(", ", headers)}.");""",
     """            // Lo que el archivo trae se devuelve acotado: es contenido del cliente.
            throw new SourceSchemaException(Notice.Of(
                NoticeCodes.CsvMissingColumns,
                ("missing", string.Join(", ", missing)),
                ("found", string.Join(", ", headers.Take(MaxEchoedHeaders).Select(Bounded)))));"""),
    ("""        var errors = new List<string>();""", """        var errors = new List<Notice>();"""),
    ("""            return (null, new SourceRejection(
                rowNumber,
                $"Sin valor en la columna clave '{_schema.KeyColumn}'.",
                null));""",
     """            return (null, new SourceRejection(
                rowNumber,
                Notice.Of(NoticeCodes.RowMissingKey, ("column", _schema.KeyColumn)),
                null));"""),
    ("""            return (null, new SourceRejection(rowNumber, string.Join(" ", errors), key));""",
     """            // El primero, con cuantos mas hubo: una fila con doce columnas malas no
            // necesita doce lineas en la pantalla de linaje para entenderse.
            var reason = errors.Count == 1 ? errors[0] : errors[0].With("more", errors.Count - 1);

            return (null, new SourceRejection(rowNumber, reason, key));"""),
    ("""        return (new Row(rowNumber, values), null);
    }
}""",
     """        return (new Row(rowNumber, values), null);
    }

    private static string Bounded(string header)
        => header.Length > MaxEchoedHeaderLength ? header[..MaxEchoedHeaderLength] + "…" : header;
}"""),
])
ensure_using("src/system/Modernization.Sources/Files/CsvSourceReader.cs", "Modernization.Domain")

# ------------------------------------------------------- SourceFileMatcher.cs
patch("src/system/Modernization.Sources/Files/SourceFileMatcher.cs", [
    ("""            return SourceFileMatch.Unresolved([], _catalog.ExpectedFileNames, ex.Message);""",
     """            return SourceFileMatch.Unresolved(
                [],
                _catalog.ExpectedFileNames,
                ex.Notice ?? Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)));"""),
    ("""                    "la fuente asignada no existe en el catalogo.");""",
     """                    Notice.Of(NoticeCodes.MatchAssignmentUnknown));"""),
    ("""            viable.Count > 1
                ? "su encabezado encaja con mas de una fuente. Asignala a mano antes de publicar."
                : "su encabezado no corresponde a ninguna fuente conocida.");""",
     """            viable.Count > 1 ? Notice.Of(NoticeCodes.MatchAmbiguous) : Notice.Of(NoticeCodes.MatchUnrecognized));"""),
    ("""    IReadOnlyList<string> Candidates,
    string? Problem)""",
     """    IReadOnlyList<string> Candidates,
    Notice? Problem)"""),
    ("""        IReadOnlyList<string> candidates,
        string problem)""",
     """        IReadOnlyList<string> candidates,
        Notice problem)"""),
])
ensure_using("src/system/Modernization.Sources/Files/SourceFileMatcher.cs", "Modernization.Domain")

# ------------------------------------------------------------ UploadService.cs
patch("src/system/Modernization.Sources/Files/UploadService.cs", [
    ("""return new PublishOutcome(false, null, [], ["No se recibio ningun archivo."], 0);""",
     """return new PublishOutcome(false, null, [], [Notice.Of(NoticeCodes.UploadNoFiles)], 0);"""),
    ("""[$"La carga pesa {Megabytes(totalBytes)} MB y el maximo es {Megabytes(MaxTotalBytes)} MB."],""",
     """[Notice.Of(NoticeCodes.UploadTooLarge, ("size", Megabytes(totalBytes)), ("max", Megabytes(MaxTotalBytes)))],"""),
    ("""["Hay otra carga publicandose en este momento. Intenta de nuevo cuando termine."],""",
     """[Notice.Of(NoticeCodes.UploadBusy)],"""),
    ("""var errors = new List<string>();""", """var errors = new List<Notice>();"""),
    ("""errors.Add($"{outcome.ClientFileName}: {outcome.Error}");""",
     """errors.Add(outcome.Error.With("file", outcome.ClientFileName));"""),
    ("""                errors.Add(
                    "Falta eai_master.csv. Es el inventario de aplicaciones: sin el no hay portafolio que puntuar.");""",
     """                // El maestro EAI es el inventario: sin el no hay portafolio que puntuar.
                errors.Add(Notice.Of(NoticeCodes.UploadMissingMaster, ("master", "eai_master.csv")));"""),
    ("""return new PublishOutcome(false, null, outcomes, [ex.Message], 0);""",
     """return new PublishOutcome(false, null, outcomes, [ex.Notice ?? Notice.Of(NoticeCodes.UploadEmpty)], 0);"""),
    ("""$"pesa {Megabytes(file.Length)} MB y el maximo por archivo es {Megabytes(MaxFileBytes)} MB.");""",
     """Notice.Of(NoticeCodes.FileTooLarge, ("size", Megabytes(file.Length)), ("max", Megabytes(MaxFileBytes))));"""),
    ("""$"duplica un archivo ya recibido para la fuente '{schema.Source}'.")""",
     """Notice.Of(NoticeCodes.FileDuplicate, ("source", schema.Source)))"""),
    ("""return new PublishedFileOutcome(clientName, schema.FileName, schema.Source, file.Length, 0, 0, ex.Message)""",
     """return new PublishedFileOutcome(
                clientName,
                schema.FileName,
                schema.Source,
                file.Length,
                0,
                0,
                ex.Notice ?? Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)))"""),
    ("""            throw new SourceSchemaException(
                "La carga no produjo ninguna aplicacion. Reemplazar el portafolio por uno vacio "
                + "seria peor que no cargar nada, asi que se descarta.");""",
     """            // Reemplazar el portafolio por uno vacio seria peor que no cargar nada.
            throw new SourceSchemaException(Notice.Of(NoticeCodes.UploadEmpty));"""),
])
ensure_using("src/system/Modernization.Sources/Files/UploadService.cs", "Modernization.Domain")

# ------------------------------------------------ DatabaseSnapshotPublisher.cs
patch("src/system/Modernization.Sources.Database/DatabaseSnapshotPublisher.cs", [
    ("""return PublishOutcome.Rejected("No se recibio ningun archivo.");""",
     """return PublishOutcome.Rejected(Notice.Of(NoticeCodes.UploadNoFiles));"""),
    ("""$"La carga pesa {Megabytes(totalBytes)} MB y el maximo es {Megabytes(PublishLimits.MaxTotalBytes)} MB.");""",
     """Notice.Of(
                    NoticeCodes.UploadTooLarge,
                    ("size", Megabytes(totalBytes)),
                    ("max", Megabytes(PublishLimits.MaxTotalBytes))));"""),
    (""""Hay otra carga publicandose en este momento. Intenta de nuevo cuando termine.");""",
     """Notice.Of(NoticeCodes.UploadBusy));"""),
    ("""var errors = new List<string>();""", """var errors = new List<Notice>();"""),
    ("""errors.Add($"{outcome.ClientFileName}: {outcome.Error}");""",
     """errors.Add(outcome.Error.With("file", outcome.ClientFileName));"""),
    ("""                errors.Add(
                    "Falta eai_master.csv. Es el inventario de aplicaciones: sin el no hay portafolio que puntuar.");""",
     """                errors.Add(Notice.Of(NoticeCodes.UploadMissingMaster, ("master", "eai_master.csv")));"""),
    ("""                errors.Add(
                    "La carga no produjo ninguna aplicacion. Reemplazar el portafolio por uno vacio "
                    + "seria peor que no cargar nada, asi que se descarta.");""",
     """                errors.Add(Notice.Of(NoticeCodes.UploadEmpty));"""),
    ("""$"pesa {Megabytes(file.Length)} MB y el maximo por archivo es {Megabytes(PublishLimits.MaxFileBytes)} MB.");""",
     """Notice.Of(
                    NoticeCodes.FileTooLarge,
                    ("size", Megabytes(file.Length)),
                    ("max", Megabytes(PublishLimits.MaxFileBytes))));"""),
    ("""$"duplica un archivo ya recibido para la fuente '{schema.Source}'.")""",
     """Notice.Of(NoticeCodes.FileDuplicate, ("source", schema.Source)))"""),
    ("""return new PublishedFileOutcome(clientName, schema.FileName, schema.Source, file.Length, 0, 0, ex.Message)""",
     """return new PublishedFileOutcome(
                clientName,
                schema.FileName,
                schema.Source,
                file.Length,
                0,
                0,
                ex.Notice ?? Notice.Of(NoticeCodes.CsvUnreadable, ("row", 1)))"""),
    ("""_ => throw new SourceSchemaException($"El contrato '{schema.FileName}' no tiene mapeo a la base."),""",
     """_ => throw new SourceSchemaException(Notice.Of(NoticeCodes.ContractUnmapped, ("file", schema.FileName))),"""),
    ("""Reject(schema, row.Number, "La fila no pudo convertirse al modelo canonico.", row.Text(schema.KeyColumn));""",
     """Reject(schema, row.Number, Notice.Of(NoticeCodes.RowNotConvertible), row.Text(schema.KeyColumn));"""),
    ("""private void Reject(SourceSchema schema, int rowNumber, string reason, string? key)""",
     """private void Reject(SourceSchema schema, int rowNumber, Notice reason, string? key)"""),
    ("""                Reason = reason,""", """                Reason = NoticeStorage.Serialize(reason),"""),
])
ensure_using("src/system/Modernization.Sources.Database/DatabaseSnapshotPublisher.cs", "Modernization.Domain")

# ------------------------------------------------- DatabaseSnapshotProvider.cs
patch("src/system/Modernization.Sources.Database/DatabaseSnapshotProvider.cs", [
    ("""new NoSnapshotAvailableException(
                "Todavia no hay ninguna carga publicada. Sube los archivos de las fuentes "
                + "para calcular el portafolio.")""",
     """new NoSnapshotAvailableException()"""),
    ("""?? throw new SourceSchemaException($"No existe la carga '{versionId}'.")""",
     """?? throw new SourceSchemaException($"The upload '{versionId}' does not exist.")"""),
    ("""new SourceRejection(r.RowNumber, r.Reason, r.Key)""",
     """new SourceRejection(r.RowNumber, NoticeStorage.Deserialize(r.Reason), r.Key)"""),
])

# ------------------------------------------------------ FileSnapshotProvider.cs
patch("src/system/Modernization.Sources/Files/FileSnapshotProvider.cs", [
    ("""new NoSnapshotAvailableException(
                "Todavia no hay ninguna carga publicada. Sube los archivos de las fuentes "
                + "para calcular el portafolio.")""",
     """new NoSnapshotAvailableException()"""),
    ("""?? throw new SourceSchemaException($"No existe la carga '{versionId}'.")""",
     """?? throw new SourceSchemaException($"The upload '{versionId}' does not exist.")"""),
    ("""                    "La fila no pudo convertirse al modelo canonico.",""",
     """                    Notice.Of(NoticeCodes.RowNotConvertible),"""),
])
ensure_using("src/system/Modernization.Sources/Files/FileSnapshotProvider.cs", "Modernization.Domain")

patch("src/system/Modernization.Sources/SnapshotAssembler.cs", [
    ("""FormattableString.Invariant($"El EAI '{key}' no existe en el maestro EAI"),""",
     """Notice.Of(NoticeCodes.RowUnknownEai, ("key", key)),"""),
])

patch("src/system/Modernization.Sources/Dummy/DummySnapshotProvider.cs", [
    (""""No existe esa carga en el portafolio dummy.\"""", """"The dummy portfolio has no such upload.\""""),
])

# ------------------------------------------------------------ IngestionService
patch("src/system/Modernization.SystemApi/Services/IngestionService.cs", [
    ("""[$"Una carga admite hasta {MaxFiles} archivos; se recibieron {files.Count}."],""",
     """[Notice.Of(NoticeCodes.UploadTooManyFiles, ("max", MaxFiles), ("count", files.Count))],"""),
    ("""if (!UploadLabel.TryNormalize(label, out var normalizedLabel, out var labelError))""",
     """if (!UploadLabel.TryNormalize(label, out var normalizedLabel, out _))"""),
    ("""return new PublishOutcome(false, null, [], [labelError!], 0);""",
     """return new PublishOutcome(
                false,
                null,
                [],
                [Notice.Of(NoticeCodes.UploadLabelInvalid, ("max", UploadLabel.MaxLength))],
                0);"""),
    ("""["La asignacion de fuentes no corresponde con los archivos enviados."],""",
     """[Notice.Of(NoticeCodes.UploadAssignmentMisaligned)],"""),
    ("""var rejected = new List<string>();""", """var rejected = new List<Notice>();"""),
    ("""rejected.Add("Se recibio un archivo sin nombre.");""", """rejected.Add(Notice.Of(NoticeCodes.FileNoName));"""),
    ("""rejected.Add($"'{name}': solo se aceptan archivos {AllowedExtension}.");""",
     """rejected.Add(Notice.Of(NoticeCodes.FileExtension, ("extension", AllowedExtension), ("file", name)));"""),
    ("""rejected.Add($"'{name}': tipo de contenido '{file.ContentType}' no aceptado.");""",
     """rejected.Add(Notice.Of(NoticeCodes.FileContentType, ("contentType", file.ContentType), ("file", name)));"""),
    ("""rejected.Add($"'{name}': la fuente asignada no tiene forma de nombre de archivo.");""",
     """rejected.Add(Notice.Of(NoticeCodes.FileAssignmentShape, ("file", name)));"""),
    ("""["El adaptador activo no admite cargas. Configura 'DataSource:Provider' en 'File'."],""",
     """[Notice.Of(NoticeCodes.UploadNotSupported)],"""),
    ("""normalizedLabel ?? $"Carga {now:dd/MM/yyyy HH:mm}",""",
     """normalizedLabel ?? FormattableString.Invariant($"Upload {now:yyyy-MM-dd HH:mm}"),"""),
])
ensure_using("src/system/Modernization.SystemApi/Services/IngestionService.cs", "Modernization.Domain")
