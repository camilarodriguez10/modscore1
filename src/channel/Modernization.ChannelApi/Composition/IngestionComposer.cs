using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Modernization.ChannelApi.Auth;
using Modernization.ChannelApi.Clients;
using Modernization.ChannelApi.ViewModels;
using Modernization.SystemApi.Contracts;
using Modernization.Domain;

namespace Modernization.ChannelApi.Composition;

/// <summary>
/// Arma la pantalla de cargas y reenvia los archivos a la capa System.
/// </summary>
/// <remarks>
/// La capa Channel no interpreta el contenido de los archivos: los reenvia como
/// stream y muestra el veredicto. Validar el esquema aqui duplicaria la logica y
/// abriria la puerta a que las dos capas discrepen sobre que es valido.
/// </remarks>
public sealed class IngestionComposer(SystemApiClient system, ChannelPermissions permissions)
{
    public async Task<IngestionView> ComposeAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var capabilitiesTask = system.GetIngestionCapabilitiesAsync(cancellationToken);
        var expectedTask = system.GetExpectedFilesAsync(cancellationToken);
        var versionsTask = system.GetVersionsAsync(cancellationToken);
        var historyTask = system.GetPortfolioHistoryAsync(cancellationToken);

        await Task.WhenAll(capabilitiesTask, expectedTask, versionsTask, historyTask).ConfigureAwait(false);

        var capabilities = await capabilitiesTask.ConfigureAwait(false);
        var expected = await expectedTask.ConfigureAwait(false);
        var versions = await versionsTask.ConfigureAwait(false);
        var history = await historyTask.ConfigureAwait(false);

        // El adaptador lo declara la capa System. Deducirlo de la primera version
        // deshabilitaba la carga justo cuando no hay ninguna: el sistema quedaba
        // bloqueado en su primer arranque, sin salida desde la interfaz.
        var provider = capabilities.Provider;

        // La pantalla no ofrece lo que el servidor va a negar: sin permiso, el
        // formulario no aparece y el motivo lo explica. La politica del endpoint
        // sigue siendo la que decide.
        var canUpload = permissions.CanUpload(user);
        var uploadEnabled = capabilities.UploadEnabled && canUpload;

        Notice? disabledReason = null;

        if (!capabilities.UploadEnabled)
        {
            disabledReason = Notice.Of(NoticeCodes.UploadDisabledAdapter, ("provider", provider));
        }
        else if (!canUpload)
        {
            disabledReason = Notice.Of(NoticeCodes.UploadDisabledPermission, ("role", ChannelPermissions.UploadRole));
        }

        return new IngestionView(
            uploadEnabled,
            provider,
            disabledReason,
            [.. expected.Select(f => new ExpectedFileView(
                f.Source,
                f.FileName,
                f.Description,
                f.KeyColumn,
                f.RequiredColumns,
                f.OptionalColumns.Count))],
            [.. versions.Select(ToVersion)],
            WithDeltas(history),
            ChannelLimits.UploadMaxFileBytes,
            ChannelLimits.UploadMaxTotalBytes);
    }

    public async Task<IReadOnlyList<HistoryPointView>> ComposeHistoryAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
    {
        var history = await system.GetApplicationHistoryAsync(eaiCode, cancellationToken).ConfigureAwait(false);

        return WithDeltas(history);
    }

    /// <summary>
    /// Reenvia la carga a la capa System. <paramref name="assignments"/> lleva, en el
    /// mismo orden que los archivos, la fuente que el usuario asigno a mano cuando el
    /// encabezado no bastaba para identificarla.
    /// </summary>
    public async Task<UploadResultView> UploadAsync(
        IFormFileCollection files,
        string? label,
        IReadOnlyList<string>? assignments = null,
        IReadOnlyList<string>? columnMaps = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        using var content = new MultipartFormDataContent();
        var streams = new List<Stream>(files.Count);

        try
        {
            foreach (var file in files)
            {
                var stream = file.OpenReadStream();
                streams.Add(stream);

                var part = new StreamContent(stream);
                part.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType ?? "text/csv");

                // Solo el nombre, nunca la ruta que envio el cliente.
                content.Add(part, "files", Path.GetFileName(file.FileName));
            }

            // Una asignacion por archivo y en el mismo orden: la capa System las
            // empareja por posicion, asi que se reenvian todas o ninguna.
            foreach (var assignment in assignments ?? [])
            {
                content.Add(new StringContent(assignment), "assign");
            }

            // El mapping de columnas va igual: uno por archivo, en el mismo orden.
            foreach (var columnMap in columnMaps ?? [])
            {
                content.Add(new StringContent(columnMap), "map");
            }

            if (!string.IsNullOrWhiteSpace(label))
            {
                content.Add(new StringContent(label.Trim()), "label");
            }

            var outcome = await system.UploadAsync(content, cancellationToken).ConfigureAwait(false);

            return ToResult(outcome);
        }
        finally
        {
            foreach (var stream in streams)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>La pestaña Mapping: de dónde sale cada campo, con la columna de la carga vigente.</summary>
    public async Task<MappingCatalogView> ComposeMappingAsync(CancellationToken cancellationToken = default)
    {
        var mapping = await system.GetMappingAsync(cancellationToken).ConfigureAwait(false);

        return new MappingCatalogView(
            mapping.Recorded,
            [.. mapping.Sources.Select(s => new MappingSourceView(
                s.Source,
                s.FileName,
                s.Description,
                s.KeyColumn,
                [.. s.Fields.Select(f => new MappingCatalogFieldView(
                    f.Column,
                    f.Type,
                    f.Required,
                    f.IsKey,
                    f.AcceptedNames,
                    f.CurrentHeader,
                    f.CurrentManual,
                    f.Feeds))]))]);
    }

    /// <summary>
    /// El mapping propuesto para cada archivo, antes de cargar. Llegan solo encabezados;
    /// la capa System identifica la fuente, resuelve cada campo y valida.
    /// </summary>
    public async Task<MappingReviewView> PreviewMappingAsync(
        IReadOnlyList<Endpoints.MappingReviewFileBody> files,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var request = new MappingPreviewRequest(
            [.. files.Select(f => new MappingPreviewFileRequest(
                f.FileName is null ? null : Path.GetFileName(f.FileName),
                f.Header,
                f.AssignedFileName,
                f.Columns))]);

        var response = await system.PreviewMappingAsync(request, cancellationToken).ConfigureAwait(false);

        return new MappingReviewView([.. response.Files.Select(f => new MappingFileView(
            f.FileName,
            f.CanonicalFileName,
            f.Source,
            f.Description,
            f.Problem,
            f.Candidates,
            f.Headers,
            [.. f.Fields.Select(c => new MappingFieldView(c.Column, c.Type, c.Required, c.IsKey, c.Header, c.Match, c.Feeds))],
            f.UnusedHeaders,
            f.Problems))]);
    }

    // -----------------------------------------------------------------------

    private static VersionView ToVersion(VersionResponse v)
        => new(v.Id, v.Label, v.CapturedAt, v.ApplicationCount, v.IsCurrent);

    /// <summary>
    /// Anade la variacion respecto al punto anterior. Es lo que convierte una
    /// serie de numeros en una tendencia legible.
    /// </summary>
    private static List<HistoryPointView> WithDeltas(IReadOnlyList<HistoryPointResponse> points)
    {
        var result = new List<HistoryPointView>(points.Count);
        decimal? previous = null;

        foreach (var point in points)
        {
            result.Add(new HistoryPointView(
                point.VersionId,
                point.Label,
                point.CapturedAt,
                point.Score,
                point.AvailableWeight,
                previous is { } before && point.Score is { } current
                    ? Math.Round(current - before, 2, MidpointRounding.AwayFromZero)
                    : null));

            previous = point.Score ?? previous;
        }

        return result;
    }

    private static UploadResultView ToResult(UploadResponse outcome) => new(
        outcome.Accepted,
        outcome.Version is null ? null : ToVersion(outcome.Version),
        outcome.ApplicationCount,
        [.. outcome.Files.Select(f => new UploadFileView(
            f.ClientFileName,
            f.CanonicalFileName,
            f.Source,
            f.Bytes,
            f.RowCount,
            f.RejectedCount,
            f.Error,
            f.DetectedColumns,
            f.Candidates,
            [.. (f.RejectionReasons ?? []).Select(r => new UploadRejectionView(r.Reason, r.Rows, r.SampleRows))],
            [.. (f.IgnoredValues ?? []).Select(r => new UploadRejectionView(r.Reason, r.Rows, r.SampleRows))]))],
        outcome.Errors,
        Summarize(outcome));

    /// <summary>
    /// Una frase de veredicto. La lee alguien que acaba de subir ocho archivos y
    /// necesita saber en un segundo si quedaron o no.
    /// </summary>
    private static Notice Summarize(UploadResponse outcome)
    {
        if (!outcome.Accepted)
        {
            // Un archivo sin fuente identificada tiene salida concreta —asignarla— y
            // el aviso la ofrece en lugar de dejar al usuario mirando un rechazo.
            var unidentified = outcome.Files.Count(f => f.CanonicalFileName is null && f.Candidates.Count > 0);

            return unidentified > 0
                ? Notice.Of(NoticeCodes.UploadRejectedUnidentified, ("count", unidentified))
                : Notice.Of(NoticeCodes.UploadRejected, ("count", outcome.Errors.Count));
        }

        return Notice.Of(
            NoticeCodes.UploadPublished,
            ("files", outcome.Files.Count),
            ("rows", outcome.Files.Sum(f => f.RowCount)),
            ("applications", outcome.ApplicationCount),
            ("rejectedRows", outcome.Files.Sum(f => f.RejectedCount)));
    }
}
