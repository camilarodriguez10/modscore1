using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Modernization.Rules;
using Modernization.SystemApi.Contracts;

namespace Modernization.ChannelApi.Clients;

/// <summary>
/// Cliente tipado de la capa System. Es el unico punto por el que la capa Channel
/// obtiene datos: no lee archivos, no consulta bases de datos y no calcula scores.
/// </summary>
/// <remarks>
/// Se referencian los contratos de la capa System en lugar de redeclararlos para
/// que un cambio de contrato rompa la compilacion, no la produccion.
/// </remarks>
public sealed class SystemApiClient(HttpClient http, IHttpClientFactory clients)
{
    /// <summary>
    /// Cliente propio de la carga: sin reintentos y con el tiempo que necesita un
    /// archivo grande.
    /// </summary>
    public const string UploadClientName = "SystemApi.Upload";

    /// <summary>Tope del detalle de error que se acepta de la capa System.</summary>
    private const int MaxProblemDetailLength = 500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<PagedResponse<ApplicationResponse>> GetApplicationsAsync(
        ApplicationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var url = $"/system/applications{query.ToQueryString()}";
        return await GetRequiredAsync<PagedResponse<ApplicationResponse>>(url, cancellationToken).ConfigureAwait(false);
    }

    public Task<ApplicationResponse?> GetApplicationAsync(string eaiCode, CancellationToken cancellationToken = default)
        => GetOrNullAsync<ApplicationResponse>($"/system/applications/{Uri.EscapeDataString(eaiCode)}", cancellationToken);

    public Task<ScoreResponse?> GetScoreAsync(string eaiCode, CancellationToken cancellationToken = default)
        => GetOrNullAsync<ScoreResponse>($"/system/applications/{Uri.EscapeDataString(eaiCode)}/score", cancellationToken);

    public Task<PagedResponse<ScoreResponse>> GetScoresAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        => GetRequiredAsync<PagedResponse<ScoreResponse>>(
            FormattableString.Invariant($"/system/scores?page={page}&pageSize={pageSize}"),
            cancellationToken);

    /// <summary>
    /// Estado de las fuentes en una carga. Sin version, la vigente: el linaje pasa a
    /// ser por carga porque comparar dos cargas es la unica forma de ver si una
    /// fuente mejoro o se degrado.
    /// </summary>
    /// <remarks>Null cuando la carga pedida no existe: es un 404, no un fallo del servicio.</remarks>
    public Task<IReadOnlyList<SourceStatusResponse>?> GetSourcesAsync(
        string? versionId = null,
        CancellationToken cancellationToken = default)
        => GetOrNullAsync<IReadOnlyList<SourceStatusResponse>>(
            string.IsNullOrWhiteSpace(versionId)
                ? "/system/sources"
                : $"/system/sources?version={Uri.EscapeDataString(versionId)}",
            cancellationToken);

    public Task<RulesetResponse> GetRulesetAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<RulesetResponse>("/system/ruleset", cancellationToken);

    public Task<IReadOnlyList<VersionResponse>> GetVersionsAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<VersionResponse>>("/system/versions", cancellationToken);

    public Task<IReadOnlyList<HistoryPointResponse>> GetApplicationHistoryAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<HistoryPointResponse>>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/history",
            cancellationToken);

    public Task<IReadOnlyList<HistoryPointResponse>> GetPortfolioHistoryAsync(
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<HistoryPointResponse>>("/system/history/portfolio", cancellationToken);

    public Task<IngestionCapabilitiesResponse> GetIngestionCapabilitiesAsync(
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IngestionCapabilitiesResponse>("/system/ingestion/capabilities", cancellationToken);

    public Task<IReadOnlyList<ExpectedFileResponse>> GetExpectedFilesAsync(
        CancellationToken cancellationToken = default)
        => GetRequiredAsync<IReadOnlyList<ExpectedFileResponse>>(
            "/system/ingestion/expected-files",
            cancellationToken);

    /// <summary>
    /// Reenvia la carga a la capa System como multipart. El contenido no se
    /// bufferiza: se copia stream a stream, por el cliente sin reintentos.
    /// </summary>
    public async Task<UploadResponse> UploadAsync(
        MultipartFormDataContent content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var client = clients.CreateClient(UploadClientName);
        using var response = await client.PostAsync(
            new Uri("/system/ingestion/upload", UriKind.Relative),
            content,
            cancellationToken).ConfigureAwait(false);

        ThrowForKnownFailures(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<UploadResponse>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new SystemApiException("The System layer returned no upload result.");
    }

    /// <summary>De dónde sale cada campo de la base, con la columna de la carga vigente.</summary>
    public async Task<MappingCatalogResponse> GetMappingAsync(CancellationToken cancellationToken = default)
        => await GetOrNullAsync<MappingCatalogResponse>("/system/ingestion/mapping", cancellationToken).ConfigureAwait(false)
            ?? throw new SystemApiException("The System layer returned no column mapping.");

    /// <summary>
    /// El mapping propuesto para cada archivo, a partir de sus encabezados. La capa System
    /// resuelve y valida; aquí solo se reenvía.
    /// </summary>
    public async Task<MappingPreviewResponse> PreviewMappingAsync(
        MappingPreviewRequest request,
        CancellationToken cancellationToken = default)
        => await PostAsync<MappingPreviewRequest, MappingPreviewResponse>(
                "/system/ingestion/mapping/preview",
                request,
                cancellationToken).ConfigureAwait(false)
            ?? throw new SystemApiException("The System layer returned no mapping review.");

    public Task<RoadmapPlanResponse?> GetRoadmapPlanAsync(
        string eaiCode,
        CancellationToken cancellationToken = default)
        => GetOrNullAsync<RoadmapPlanResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/roadmap",
            cancellationToken);

    /// <summary>
    /// El plan del portafolio filtrado y agrupado por la dimension pedida. Los
    /// filtros son los mismos del inventario: una pantalla no puede ver mas que otra
    /// con el mismo filtro puesto.
    /// </summary>
    public Task<PortfolioRoadmapResponse> GetPortfolioRoadmapAsync(
        ApplicationQuery? query = null,
        string? groupBy = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>(query?.FilterParts() ?? []);

        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            parts.Insert(0, $"groupBy={Uri.EscapeDataString(groupBy)}");
        }

        var url = parts.Count == 0
            ? "/system/roadmap/portfolio"
            : "/system/roadmap/portfolio?" + string.Join('&', parts);

        return GetRequiredAsync<PortfolioRoadmapResponse>(url, cancellationToken);
    }

    /// <summary>
    /// El dashboard ejecutivo sobre el mismo recorte que el inventario. El eje del mapa
    /// de calor viaja aparte: es una forma de mirar, no un filtro.
    /// </summary>
    public Task<DashboardResponse> GetDashboardAsync(
        ApplicationQuery? query = null,
        string? axis = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>(query?.FilterParts() ?? []);

        if (!string.IsNullOrWhiteSpace(axis))
        {
            parts.Add("axis=" + Uri.EscapeDataString(axis));
        }

        var url = parts.Count == 0 ? "/system/dashboard" : "/system/dashboard?" + string.Join('&', parts);

        return GetRequiredAsync<DashboardResponse>(url, cancellationToken);
    }

    /// <summary>La pantalla Code sobre el mismo recorte que el inventario (ADR 0026).</summary>
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

    public Task<LeverCatalogResponse?> GetLeversAsync(string eaiCode, CancellationToken cancellationToken = default)
        => GetOrNullAsync<LeverCatalogResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/whatif/levers",
            cancellationToken);

    public Task<ScenarioResponse?> PostScenarioAsync(
        string eaiCode,
        IReadOnlyList<LeverRequest> levers,
        CancellationToken cancellationToken = default)
        => PostAsync<ScenarioRequest, ScenarioResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/whatif/scenario",
            new ScenarioRequest(levers),
            cancellationToken);

    public Task<TargetResponse?> PostTargetAsync(
        string eaiCode,
        decimal targetScore,
        CancellationToken cancellationToken = default)
        => PostAsync<TargetRequest, TargetResponse>(
            $"/system/applications/{Uri.EscapeDataString(eaiCode)}/whatif/target",
            new TargetRequest(targetScore),
            cancellationToken);

    // ------------------------------------------------------ reglas (Fase 12)

    public Task<RulesStateResponse> GetRulesAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<RulesStateResponse>("/system/rules", cancellationToken);

    /// <remarks>Null cuando la version no existe: es un 404, no un fallo del servicio.</remarks>
    public Task<RulesVersionDocumentResponse?> GetRulesVersionAsync(string id, CancellationToken cancellationToken = default)
        => GetOrNullAsync<RulesVersionDocumentResponse>(
            $"/system/rules/versions/{Uri.EscapeDataString(id)}",
            cancellationToken);

    public async Task<RulesSimulationResponse> SimulateRulesAsync(
        RulesSimulationRequest request,
        CancellationToken cancellationToken = default)
        => await PostAsync<RulesSimulationRequest, RulesSimulationResponse>("/system/rules/simulate", request, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new SystemApiException("The System layer returned no simulation.");

    /// <summary>
    /// Publica un borrador. Va por el cliente estandar, que nunca reintenta un POST:
    /// un reintento publicaria dos versiones, o chocaria con la que acaba de publicar.
    /// </summary>
    public async Task<RulesPublishResponse> PublishRulesAsync(
        RulesPublishRequest request,
        CancellationToken cancellationToken = default)
        => await PostAsync<RulesPublishRequest, RulesPublishResponse>("/system/rules/publish", request, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new SystemApiException("The System layer returned no publication result.");

    /// <summary>Cuando cambiaron las reglas. Lo lee cualquiera que consulta.</summary>
    public Task<RulesTimelineResponse> GetRulesTimelineAsync(CancellationToken cancellationToken = default)
        => GetRequiredAsync<RulesTimelineResponse>("/system/rules/timeline", cancellationToken);

    /// <remarks>Null cuando alguna de las versiones, o la aplicacion pedida, no existe.</remarks>
    public Task<RulesCompareResponse?> CompareRulesAsync(
        string from,
        string to,
        string? eaiCode,
        CancellationToken cancellationToken = default)
        => GetOrNullAsync<RulesCompareResponse>("/system/rules/compare" + CompareQuery(from, to, eaiCode), cancellationToken);

    /// <remarks>Null cuando alguna de las versiones no existe.</remarks>
    public Task<IReadOnlyList<ApplicationShift>?> GetRulesComparisonRowsAsync(
        string from,
        string to,
        CancellationToken cancellationToken = default)
        => GetOrNullAsync<IReadOnlyList<ApplicationShift>>(
            "/system/rules/compare/applications" + CompareQuery(from, to, null),
            cancellationToken);

    private static string CompareQuery(string from, string to, string? eaiCode)
        => $"?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}"
            + (eaiCode is null ? string.Empty : $"&eai={Uri.EscapeDataString(eaiCode)}");

    /// <summary>
    /// Trae todas las aplicaciones que pasan el filtro, paginando.
    /// Los agregados del portafolio deben cubrir el conjunto filtrado completo,
    /// no la pagina visible.
    /// </summary>
    public async Task<IReadOnlyList<ApplicationResponse>> GetAllApplicationsAsync(
        ApplicationQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        const int PageSize = 200;
        var all = new List<ApplicationResponse>();
        var page = 1;

        while (true)
        {
            var batch = await GetApplicationsAsync(
                query with { Page = page, PageSize = PageSize },
                cancellationToken).ConfigureAwait(false);

            all.AddRange(batch.Items);

            if (page >= batch.TotalPages || batch.Items.Count == 0)
            {
                return all;
            }

            page++;
        }
    }

    /// <summary>
    /// Trae todos los scores paginando. La capa System acota el tamano de pagina
    /// a proposito, asi que agregar el portafolio completo es trabajo de esta capa.
    /// </summary>
    public async Task<IReadOnlyList<ScoreResponse>> GetAllScoresAsync(CancellationToken cancellationToken = default)
    {
        const int PageSize = 200;
        var all = new List<ScoreResponse>();
        var page = 1;

        while (true)
        {
            var batch = await GetScoresAsync(page, PageSize, cancellationToken).ConfigureAwait(false);
            all.AddRange(batch.Items);

            if (page >= batch.TotalPages || batch.Items.Count == 0)
            {
                return all;
            }

            page++;
        }
    }

    /// <summary>
    /// Un 400 de la capa System es un escenario invalido, no un fallo del servicio:
    /// se propaga con su detalle para que el usuario sepa que corregir.
    /// </summary>
    private async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string url,
        TRequest body,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var response = await http.PostAsJsonAsync(url, body, Json, cancellationToken).ConfigureAwait(false);

        ThrowForKnownFailures(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
        {
            throw new SystemApiException(
                await ReadProblemDetailAsync(response, cancellationToken).ConfigureAwait(false)
                    ?? "The System layer rejected the request.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(Json, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> GetRequiredAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        ThrowForKnownFailures(response);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false)
            ?? throw new SystemApiException($"The System layer returned an empty body for {url}.");
    }

    private async Task<T?> GetOrNullAsync<T>(string url, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

        ThrowForKnownFailures(response);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            throw new SystemApiException(
                await ReadProblemDetailAsync(response, cancellationToken).ConfigureAwait(false)
                    ?? "The System layer could not process the resource.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Respuestas de la capa System con significado propio. Un 409 es una sola cosa
    /// —no hay ninguna carga publicada— y un 403 es un recurso fuera del alcance
    /// del usuario. Ambas tienen salida en la interfaz; el resto de errores no.
    /// </summary>
    private static void ThrowForKnownFailures(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new SystemApiNoDataException();
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new SystemApiForbiddenException();
        }
    }

    /// <summary>
    /// Solo el campo <c>detail</c>, acotado. El cuerpo completo trae traceId y
    /// metadatos internos que no tienen por que llegar al navegador.
    /// </summary>
    private static async Task<string?> ReadProblemDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String)
            {
                var text = detail.GetString();
                return text is { Length: > MaxProblemDetailLength } ? text[..MaxProblemDetailLength] : text;
            }
        }
        catch (JsonException)
        {
            // Un cuerpo que no es ProblemDetails no aporta un mensaje util.
        }

        return null;
    }
}

/// <summary>
/// Filtros del portafolio. Desde la Fase 10 los comparten todas las pantallas de
/// consulta: el mismo filtro puesto en cualquiera acota igual en las demas.
/// </summary>
public sealed record ApplicationQuery
{
    public string? Lob { get; init; }
    public string? Country { get; init; }
    public string? Cio { get; init; }
    public string? Cre { get; init; }
    public string? Region { get; init; }
    public string? Status { get; init; }
    public string? Capability { get; init; }
    public string? EaiCode { get; init; }
    public string? Disposition { get; init; }
    public string? Roadmap { get; init; }

    /// <summary>Tramo de score.</summary>
    public string? Band { get; init; }

    public bool? CriticalOnly { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;

    /// <summary>
    /// Los filtros ya codificados, sin paginacion. Las pantallas que agregan no
    /// paginan, pero filtran exactamente igual.
    /// </summary>
    internal IReadOnlyList<string> FilterParts()
    {
        var parts = new List<string>(12);

        // Todo valor de usuario se codifica antes de entrar a la URL.
        Add(parts, "lob", Lob);
        Add(parts, "country", Country);
        Add(parts, "cio", Cio);
        Add(parts, "cre", Cre);
        Add(parts, "region", Region);
        Add(parts, "status", Status);
        Add(parts, "capability", Capability);
        Add(parts, "eai", EaiCode);
        Add(parts, "disposition", Disposition);
        Add(parts, "roadmap", Roadmap);
        Add(parts, "band", Band);

        if (CriticalOnly == true)
        {
            parts.Add("criticalOnly=true");
        }

        return parts;

        static void Add(List<string> target, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                target.Add(string.Create(CultureInfo.InvariantCulture, $"{name}={Uri.EscapeDataString(value)}"));
            }
        }
    }

    internal string ToQueryString()
    {
        var parts = new List<string>(9)
        {
            FormattableString.Invariant($"page={Page}"),
            FormattableString.Invariant($"pageSize={PageSize}"),
        };

        parts.AddRange(FilterParts());

        return "?" + string.Join('&', parts);
    }
}

/// <summary>No hay nada que componer porque nadie ha publicado una carga.</summary>
public sealed class SystemApiNoDataException : Exception
{
    public SystemApiNoDataException()
        : base(Modernization.Domain.Sources.NoSnapshotAvailableException.DefaultMessage) { }

    public SystemApiNoDataException(string message) : base(message) { }

    public SystemApiNoDataException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>La capa System nego el recurso: esta fuera del alcance del usuario.</summary>
public sealed class SystemApiForbiddenException : Exception
{
    public SystemApiForbiddenException()
        : base("The resource is outside the user's scope.") { }

    public SystemApiForbiddenException(string message) : base(message) { }

    public SystemApiForbiddenException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>La capa System respondio algo que la capa Channel no puede componer.</summary>
public sealed class SystemApiException : Exception
{
    public SystemApiException(string message) : base(message) { }

    public SystemApiException(string message, Exception innerException) : base(message, innerException) { }

    public SystemApiException() { }
}
