from patchlib import patch

patch(r"src\channel\Modernization.ChannelApi\Clients\SystemApiClient.cs", [
("""    public Task<LeverCatalogResponse?> GetLeversAsync(string eaiCode, CancellationToken cancellationToken = default)""",
"""    /// <summary>La pantalla Code sobre el mismo recorte que el inventario (ADR 0026).</summary>
    public Task<CodeResponse> GetCodeAsync(ApplicationQuery? query = null, CancellationToken cancellationToken = default)
    {
        var parts = query?.FilterParts() ?? [];
        var url = parts.Count == 0 ? "/system/code" : "/system/code?" + string.Join('&', parts);

        return GetRequiredAsync<CodeResponse>(url, cancellationToken);
    }

    /// <summary>Los repositorios de una aplicación. Null si no existe o está fuera del alcance.</summary>
    public Task<CodeApplicationResponse?> GetCodeApplicationAsync(string eaiCode, CancellationToken cancellationToken = default)
        => GetOrNullAsync<CodeApplicationResponse>(
            $"/system/code/applications/{Uri.EscapeDataString(eaiCode)}",
            cancellationToken);

    public Task<LeverCatalogResponse?> GetLeversAsync(string eaiCode, CancellationToken cancellationToken = default)"""),
])

patch(r"src\channel\Modernization.ChannelApi\Endpoints\ChannelEndpoints.cs", [
("""        api.MapGet("/insights", GetInsights)
            .WithSummary("Pantalla 09 — las seis preguntas del portafolio, con los mismos filtros.");
""",
"""        api.MapGet("/insights", GetInsights)
            .WithSummary("Pantalla 09 — las seis preguntas del portafolio, con los mismos filtros.");

        api.MapGet("/code", GetCode)
            .WithSummary("Pantalla Code — obsolescencia y riesgo segun los repositorios, con los mismos filtros.");

        api.MapGet("/code/applications/{eaiCode}", GetCodeApplication)
            .WithSummary("Repositorios, componentes y dependencias de una aplicacion.");
"""),
("""    private static async Task<IResult> GetPortfolioRoadmap(""",
"""    /// <summary>
    /// La pantalla Code. Como el dashboard, la capa Channel no calcula: valida los filtros,
    /// pregunta y entrega (ADR 0026).
    /// </summary>
    private static async Task<IResult> GetCode(
        SystemApiClient system,
        [AsParameters] FilterParameters filters,
        CancellationToken cancellationToken)
    {
        if (InvalidFilters(filters) is { } problem)
        {
            return problem;
        }

        var code = await system.GetCodeAsync(filters.ToQuery(), cancellationToken).ConfigureAwait(false);

        return Results.Ok(code);
    }

    private static async Task<IResult> GetCodeApplication(
        string eaiCode,
        SystemApiClient system,
        CancellationToken cancellationToken)
    {
        if (!InputValidation.IsValidEaiCode(eaiCode))
        {
            return InvalidEaiCode();
        }

        var detail = await system.GetCodeApplicationAsync(eaiCode, cancellationToken).ConfigureAwait(false);

        return detail is null ? NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> GetPortfolioRoadmap("""),
])
