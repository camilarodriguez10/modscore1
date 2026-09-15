import io, os
root = r"C:\Users\kmiro\Downloads\mod score"

def edit(rel, pairs):
    p = os.path.join(root, rel)
    s = io.open(p, encoding='utf-8', newline='').read()
    crlf = '\r\n' in s
    if crlf:
        s = s.replace('\r\n', '\n')
    for old, new in pairs:
        assert old in s, f"NO MATCH in {rel}: {old[:90]!r}"
        s = s.replace(old, new, 1)
    if crlf:
        s = s.replace('\n', '\r\n')
    io.open(p, 'w', encoding='utf-8', newline='').write(s)
    print("ok", rel)

# ------------------------------------------------------------ contrato
edit(r"src\system\Modernization.SystemApi\Contracts\SystemContracts.cs", [
    ("""/// <summary>Un archivo que la carga espera, con sus columnas obligatorias.</summary>""",
     """/// <summary>
/// Que adaptador esta activo y si admite cargas. Lo declara la capa System: no
/// se deduce de los datos, porque sin ninguna carga no hay datos de donde
/// deducirlo — y ese es justamente el momento en que la carga tiene que estar
/// disponible.
/// </summary>
public sealed record IngestionCapabilitiesResponse(string Provider, bool UploadEnabled);

/// <summary>Un archivo que la carga espera, con sus columnas obligatorias.</summary>"""),
])

# ------------------------------------------------------------ endpoint
edit(r"src\system\Modernization.SystemApi\Endpoints\IngestionEndpoints.cs", [
    ("""        group.MapGet("/ingestion/expected-files", GetExpectedFiles)
            .WithSummary("Archivos que la carga espera, con sus columnas obligatorias.");""",
     """        group.MapGet("/ingestion/expected-files", GetExpectedFiles)
            .WithSummary("Archivos que la carga espera, con sus columnas obligatorias.");

        group.MapGet("/ingestion/capabilities", GetCapabilities)
            .WithSummary("Adaptador activo y si admite cargas.");"""),
    ("""    private static IResult GetExpectedFiles(IngestionService ingestion)
        => Results.Ok(ingestion.DescribeExpectedFiles());""",
     """    private static IResult GetExpectedFiles(IngestionService ingestion)
        => Results.Ok(ingestion.DescribeExpectedFiles());

    private static IResult GetCapabilities(IApplicationSnapshotProvider provider, IngestionService ingestion)
        => Results.Ok(new IngestionCapabilitiesResponse(provider.Provider, ingestion.IsEnabled));"""),
])

# ------------------------------------------------------------ cliente
edit(r"src\channel\Modernization.ChannelApi\Clients\SystemApiClient.cs", [
    ("""    public Task<IReadOnlyList<ExpectedFileResponse>> GetExpectedFilesAsync(""",
     """    public Task<IngestionCapabilitiesResponse> GetIngestionCapabilitiesAsync(
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IngestionCapabilitiesResponse>("/system/ingestion/capabilities", cancellationToken);

    public Task<IReadOnlyList<ExpectedFileResponse>> GetExpectedFilesAsync("""),
])

# ------------------------------------------------------------ composer
edit(r"src\channel\Modernization.ChannelApi\Composition\IngestionComposer.cs", [
    ("""        var expectedTask = system.GetExpectedFilesAsync(cancellationToken);
        var versionsTask = system.GetVersionsAsync(cancellationToken);
        var historyTask = system.GetPortfolioHistoryAsync(cancellationToken);

        await Task.WhenAll(expectedTask, versionsTask, historyTask).ConfigureAwait(false);

        var expected = await expectedTask.ConfigureAwait(false);
        var versions = await versionsTask.ConfigureAwait(false);
        var history = await historyTask.ConfigureAwait(false);

        var provider = versions.Count > 0 ? versions[0].Provider : "desconocido";
        var uploadEnabled = string.Equals(provider, "File", StringComparison.Ordinal);""",
     """        var capabilitiesTask = system.GetIngestionCapabilitiesAsync(cancellationToken);
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
        var uploadEnabled = capabilities.UploadEnabled;"""),
])

print("done")
