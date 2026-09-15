import io, os
root = r"C:\Users\kmiro\Downloads\mod score"

def edit(rel, pairs):
    p = os.path.join(root, rel)
    s = io.open(p, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {rel}: {old[:90]!r}"
        s = s.replace(old, new, 1)
    io.open(p, 'w', encoding='utf-8', newline='').write(s)
    print("ok", rel)

# ------------------------------------------------------------ system-api
edit(r"src\system\Modernization.SystemApi\Program.cs", [
    ("""// Sin datos del portafolio en las respuestas de error.
builder.Services.AddProblemDetails();""",
     """// Sin datos del portafolio en las respuestas de error.
builder.Services.AddProblemDetails();

// "Todavia no hay datos" es un estado, no una falla: se responde 409 para que la
// interfaz ofrezca la carga en vez de una pantalla de error.
builder.Services.AddExceptionHandler<Modernization.SystemApi.NoDataExceptionHandler>();"""),
    ("""var app = builder.Build();

app.UseSecurityHeaders(apiOnly: true);""",
     """var app = builder.Build();

app.UseExceptionHandler();
app.UseSecurityHeaders(apiOnly: true);"""),
])

# ------------------------------------------------------------ channel client
edit(r"src\channel\Modernization.ChannelApi\Clients\SystemApiClient.cs", [
    # PostAsync
    ("""        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)""",
     """        ThrowIfNoData(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)"""),
    # GetRequiredAsync
    ("""    private async Task<T> GetRequiredAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();""",
     """    private async Task<T> GetRequiredAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        ThrowIfNoData(response);
        response.EnsureSuccessStatusCode();"""),
    # GetOrNullAsync + cierre de la clase
    ("""    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var problem = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new SystemApiException($"La capa System no pudo procesar el recurso: {problem}");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
    }
}""",
     """    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        ThrowIfNoData(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var problem = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new SystemApiException($"La capa System no pudo procesar el recurso: {problem}");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Un 409 de la capa System significa una sola cosa: todavia no hay ninguna
    /// carga publicada. Se separa del resto de los errores porque tiene una salida
    /// concreta, y la interfaz puede ofrecerla en vez de una pantalla de error.
    /// </summary>
    private static void ThrowIfNoData(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new SystemApiNoDataException();
        }
    }
}

/// <summary>No hay nada que componer porque nadie ha publicado una carga.</summary>
public sealed class SystemApiNoDataException : Exception
{
    public SystemApiNoDataException()
        : base("Todavia no hay ninguna carga publicada. Sube los archivos de las fuentes "
               + "para calcular el portafolio.") { }

    public SystemApiNoDataException(string message) : base(message) { }

    public SystemApiNoDataException(string message, Exception innerException)
        : base(message, innerException) { }
}"""),
])

print("done")
