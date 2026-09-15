import io, os

def edit(path, pairs):
    s = io.open(path, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {path}: {old[:80]!r}"
        s = s.replace(old, new)
    io.open(path, 'w', encoding='utf-8').write(s)
    print("ok", os.path.basename(path))

root = r"C:\Users\kmiro\Downloads\mod score"

# 1. Ids de carga a prueba de colisiones.
edit(os.path.join(root, r"src\system\Modernization.Sources\Files\FileSnapshotProvider.cs"), [
    ("""    /// <summary>Identificador ordenable por fecha, sin caracteres de ruta.</summary>
    public static string NewId(DateTimeOffset capturedAt)
        => capturedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);""",

     """    /// <summary>
    /// Identificador ordenable por fecha, sin caracteres de ruta.
    /// </summary>
    /// <remarks>
    /// Lleva un sufijo aleatorio ademas de la marca de tiempo: con granularidad
    /// de segundos, dos cargas simultaneas producirian el mismo id y la segunda
    /// sobrescribiria a la primera sin aviso.
    /// </remarks>
    public static string NewId(DateTimeOffset capturedAt)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{capturedAt.UtcDateTime:yyyyMMdd-HHmmss}-{Random.Shared.Next(0x1000, 0xFFFF):x4}");"""),
])

# 2. La prueba de nombre desconocido pertenece al fixture con adaptador de
#    archivos: con Dummy la carga se corta antes de llegar a la lista blanca.
edit(os.path.join(root, r"tests\Modernization.Api.Tests\UploadSecurityTests.cs"), [
    ("""    [Fact]
    public async Task UnknownFileNameIsRejectedWithTheExpectedList()
    {
        var client = await MutatingClientAsync();

        using var form = Form("inventario-random.csv", ValidCsv);
        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Contains("eai_master.csv", StringComparison.Ordinal));
    }

""", ""),
])

# 3. Los tests de flujo no pueden depender de conteos absolutos: cada uno publica
#    su propia version sobre el mismo fixture.
edit(os.path.join(root, r"tests\Modernization.Api.Tests\UploadFlowTests.cs"), [
    ("""        var before = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);
        Assert.NotNull(before);

        using var form = SeedForm("Carga de prueba");""",

     """        var before = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);
        Assert.NotNull(before);

        using var form = SeedForm("Carga de prueba");
"""),

    ("""        Assert.NotNull(after);
        Assert.Equal(before.Versions.Count + 1, after.Versions.Count);
        Assert.Equal("Carga de prueba", after.Versions.Single(v => v.IsCurrent).Label);""",

     """        Assert.NotNull(after);
        Assert.True(after.Versions.Count > before.Versions.Count);
        Assert.Equal("Carga de prueba", after.Versions.Single(v => v.IsCurrent).Label);"""),

    ("""    [Fact]
    public async Task IngestionScreenReportsUploadEnabledOnTheFileAdapter()
    {
        var client = await fixture.CreateMutatingClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.UploadEnabled);
        Assert.Equal("File", view.Provider);
        Assert.Null(view.DisabledReason);
        Assert.Single(view.Versions);
    }""",

     """    [Fact]
    public async Task IngestionScreenReportsUploadEnabledOnTheFileAdapter()
    {
        var client = await fixture.CreateMutatingClientAsync();

        var view = await client.GetFromJsonAsync<IngestionView>(
            new Uri("/api/ingestion", UriKind.Relative), Json);

        Assert.NotNull(view);
        Assert.True(view.UploadEnabled);
        Assert.Equal("File", view.Provider);
        Assert.Null(view.DisabledReason);
        Assert.NotEmpty(view.Versions);
        Assert.Single(view.Versions, v => v.IsCurrent);
    }

    /// <summary>
    /// Un nombre que no corresponde a ninguna fuente se rechaza, y el error dice
    /// cuales se esperaban en vez de dejar al usuario adivinando.
    /// </summary>
    [Fact]
    public async Task UnknownFileNameIsRejectedWithTheExpectedList()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes("A,B\\n1,2\\n"));
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
        form.Add(part, "files", "inventario-random.csv");

        using var response = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), form);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(result);
        Assert.False(result.Accepted);
        Assert.Contains(result.Errors, e => e.Contains("eai_master.csv", StringComparison.Ordinal));
    }

    /// <summary>
    /// Dos cargas seguidas producen versiones distintas. Con ids a granularidad
    /// de segundo, la segunda habria sobrescrito a la primera sin aviso.
    /// </summary>
    [Fact]
    public async Task TwoUploadsInQuickSuccessionProduceDistinctVersions()
    {
        var client = await fixture.CreateMutatingClientAsync();

        using var firstForm = SeedForm("Rafaga A");
        using var firstResponse = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), firstForm);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<UploadResultView>(Json);

        using var secondForm = SeedForm("Rafaga B");
        using var secondResponse = await client.PostAsync(new Uri(UploadPath, UriKind.Relative), secondForm);
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<UploadResultView>(Json);

        Assert.NotNull(first?.Version);
        Assert.NotNull(second?.Version);
        Assert.NotEqual(first.Version.Id, second.Version.Id);
    }"""),
])
