import io
import os
import re

from p11_lib import ROOT, patch, ensure_using

PROBLEM = re.compile(
    r'Results\.Problem\(\s*detail:\s*(?P<detail>.+?),\s*statusCode:\s*(?P<status>[\w.]+),\s*title:\s*"(?P<title>[^"]*)"\s*\)',
    re.S,
)

# (titulo, detalle normalizado) -> (codigo, titulo en ingles, detalle en ingles)
MAPPING = {
    ("Parametros de paginacion invalidos", """$"'page' debe ser 1 o mayor y 'pageSize' debe estar entre 1 y {MaxPageSize}.\""""):
        ("invalid-pagination", "Invalid pagination parameters", """$"'page' must be 1 or greater and 'pageSize' must be between 1 and {MaxPageSize}.\""""),
    ("Filtro invalido", """$"Cada filtro admite hasta {MaxFilterLength} caracteres.\""""):
        ("invalid-filter", "Invalid filter", """$"Each filter accepts up to {MaxFilterLength} characters.\""""),
    ("Tramo invalido", """$"Los tramos de score son: {string.Join(", ", ScoreBands.All.Select(b => b.Name))}.\""""):
        ("invalid-band", "Invalid score band", """$"The score bands are: {string.Join(", ", ScoreBands.All.Select(b => b.Code))}.\""""),
    ("Carga invalida", '"El identificador de carga no tiene una forma valida."'):
        ("invalid-version", "Invalid upload identifier", '"The upload identifier is not well formed."'),
    ("Carga no encontrada", '"No existe una carga con ese identificador."'):
        ("version-not-found", "Upload not found", '"There is no upload with that identifier."'),
    ("Formato de peticion invalido", '"Envia los archivos como multipart/form-data."'):
        ("unsupported-media-type", "Invalid request format", '"Send the files as multipart/form-data."'),
    ("Carga invalida", """$"La carga no es un multipart valido o supera los {ChannelLimits.Megabytes(ChannelLimits.UploadMaxTotalBytes)} MB.\""""):
        ("invalid-upload", "Invalid upload", """$"The upload is not valid multipart content or exceeds {ChannelLimits.Megabytes(ChannelLimits.UploadMaxTotalBytes)} MB.\""""),
    ("Carga vacia", '"Selecciona al menos un archivo para cargar."'):
        ("empty-upload", "Empty upload", '"Select at least one file to upload."'),
    ("Etiqueta invalida", "labelError"):
        ("invalid-label", "Invalid label", "labelError"),
    ("Asignacion invalida", '"Envia una asignacion de fuente por archivo, en el mismo orden."'):
        ("invalid-assignment", "Invalid source assignment", '"Send one source assignment per file, in the same order."'),
    ("Asignacion invalida", """$"Una fuente asignada admite hasta {MaxAssignmentLength} caracteres.\""""):
        ("invalid-assignment", "Invalid source assignment", """$"An assigned source accepts up to {MaxAssignmentLength} characters.\""""),
    ("Agrupacion invalida", """$"Las agrupaciones disponibles son: {string.Join(", ", Groupings)}.\""""):
        ("invalid-grouping", "Invalid grouping", """$"The available groupings are: {string.Join(", ", Groupings)}.\""""),
    ("Escenario vacio", '"Elige al menos una palanca para simular."'):
        ("empty-scenario", "Empty scenario", '"Choose at least one lever to simulate."'),
    ("Escenario invalido", """$"Cada palanca se identifica por su clave del catalogo, y un escenario admite hasta {MaxLeversPerRequest}.\""""):
        ("invalid-scenario", "Invalid scenario", """$"Each lever is identified by its catalog key, and a scenario accepts up to {MaxLeversPerRequest}.\""""),
    ("Escenario invalido", "ex.Message"):
        ("invalid-scenario", "Invalid scenario", "ex.Message"),
    ("Objetivo invalido", '"El score objetivo debe estar entre 20 y 100."'):
        ("invalid-target", "Invalid target", '"The target score must be between 20 and 100."'),
    ("Objetivo invalido", "ex.Message"):
        ("invalid-target", "Invalid target", "ex.Message"),
    ("Aplicacion no encontrada", '"No existe una aplicacion con ese EAI en el portafolio cargado."'):
        ("application-not-found", "Application not found", '"There is no application with that EAI code in the loaded portfolio."'),
    ("EAI code invalido", '"Un EAI code es una secuencia de hasta 10 digitos."'):
        ("invalid-eai-code", "Invalid EAI code", '"An EAI code is a sequence of up to 10 digits."'),
    ("Carga demasiado grande", """$"La carga supera el maximo de {ChannelLimits.Megabytes(ChannelLimits.UploadMaxTotalBytes)} MB.\""""):
        ("upload-too-large", "Upload too large", """$"The upload exceeds the maximum of {ChannelLimits.Megabytes(ChannelLimits.UploadMaxTotalBytes)} MB.\""""),
    ("Alcance insuficiente", '"Publicar una carga exige alcance sobre todo el portafolio."'):
        ("insufficient-scope", "Insufficient scope", '"Publishing an upload requires scope over the whole portfolio."'),
    ("Formato de peticion invalido", '"La carga se envia como multipart/form-data."'):
        ("unsupported-media-type", "Invalid request format", '"The upload must be sent as multipart/form-data."'),
    ("Carga demasiado grande", '"La carga supera el tamano maximo admitido."'):
        ("upload-too-large", "Upload too large", '"The upload exceeds the accepted maximum size."'),
    ("Carga invalida", '"La carga no es un multipart valido o supera los limites admitidos."'):
        ("invalid-upload", "Invalid upload", '"The upload is not valid multipart content or exceeds the accepted limits."'),
    ("Agrupacion invalida", '"Agrupacion desconocida. Opciones: portfolio, application, country, cio, lob, " + "disposition, roadmap."'):
        ("invalid-grouping", "Invalid grouping", '"Unknown grouping. Options: portfolio, application, country, cio, lob, disposition, roadmap."'),
    ("Filtro invalido", "invalid"):
        ("invalid-filter", "Invalid filter", "invalid"),
    ("El dato de la aplicacion no encaja en el ruleset", "ex.Message"):
        ("ruleset-mismatch", "The application's data does not fit the ruleset",
         '"One of the application\'s values falls outside the bands declared by the active ruleset."'),
    ("Alcance insuficiente", '"El estado de las fuentes exige alcance sobre todo el portafolio."'):
        ("insufficient-scope", "Insufficient scope", '"Source status requires scope over the whole portfolio."'),
    ("Escenario vacio", '"Un escenario necesita al menos una palanca."'):
        ("empty-scenario", "Empty scenario", '"A scenario needs at least one lever."'),
}


def convert_problems(name):
    path = os.path.join(ROOT, name)
    raw = io.open(path, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")

    def repl(m):
        key = (m.group("title"), " ".join(m.group("detail").split()))
        if key not in MAPPING:
            raise AssertionError(f"UNMAPPED in {name}: {key}")
        code, title, detail = MAPPING[key]
        line_start = s.rfind("\n", 0, m.start()) + 1
        indent = re.match(r"[ ]*", s[line_start:]).group(0)
        inner = indent + "    "
        return (f'ApiProblem.Create(\n{inner}{m.group("status")},\n{inner}"{code}",\n'
                f'{inner}"{title}",\n{inner}{detail})')

    s, count = PROBLEM.subn(repl, s)
    if "Results.Problem(" in s:
        raise AssertionError(f"Results.Problem left in {name}")
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print(f"problems {count} -> {name}")


def replace_between(name, start, end, new):
    """Reemplaza desde `start` (inclusive) hasta el final de `end` (inclusive)."""
    path = os.path.join(ROOT, name)
    raw = io.open(path, encoding="utf-8", newline="").read()
    crlf = "\r\n" in raw
    s = raw.replace("\r\n", "\n")
    i = s.find(start)
    if i < 0:
        if new.strip() in s:
            return
        raise AssertionError(f"START not found in {name}: {start[:80]!r}")
    j = s.find(end, i)
    if j < 0:
        raise AssertionError(f"END not found in {name}: {end[:80]!r}")
    s = s[:i] + new + s[j + len(end):]
    if crlf:
        s = s.replace("\n", "\r\n")
    io.open(path, "w", encoding="utf-8", newline="").write(s)
    print("region ->", name)


# =============================================================== capa System
for endpoints in ["SystemEndpoints", "RoadmapEndpoints", "WhatIfEndpoints", "IngestionEndpoints"]:
    convert_problems(f"src/system/Modernization.SystemApi/Endpoints/{endpoints}.cs")

# El codigo de un escenario invalido es el de la excepcion, no uno fijo.
path = os.path.join(ROOT, "src/system/Modernization.SystemApi/Endpoints/WhatIfEndpoints.cs")
text = io.open(path, encoding="utf-8", newline="").read()
text, n = re.subn(r'"invalid-scenario",(\s*)"Invalid scenario",(\s*)ex\.Message\)', r'ex.Code,\1"Invalid scenario",\2ex.Message)', text)
assert n == 1, n
io.open(path, "w", encoding="utf-8", newline="").write(text)
print("whatif code from exception")

patch("src/system/Modernization.SystemApi/NoDataExceptionHandler.cs", [
    ("""    public const string ProblemType = "urn:modernization:no-data";""",
     """    public static readonly string ProblemType = ApiProblem.TypeOf("no-data");"""),
    ("""        await Results.Problem(
                detail: noData.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Sin cargas publicadas",
                type: ProblemType)""",
     """        await ApiProblem.Create(
                StatusCodes.Status409Conflict,
                "no-data",
                "No published uploads",
                noData.Message)"""),
])
ensure_using("src/system/Modernization.SystemApi/NoDataExceptionHandler.cs", "Modernization.Security")

patch("src/system/Modernization.SystemApi/Services/ApplicationFilter.cs", [
    ("""return $"Cada filtro admite hasta {MaxLength} caracteres.";""",
     """return $"Each filter accepts up to {MaxLength} characters.";"""),
    (""": $"Tramo desconocido. Los tramos son: {string.Join(", ", ScoreBands.All.Select(b => b.Name))}.";""",
     """: $"Unknown score band. The bands are: {string.Join(", ", ScoreBands.All.Select(b => b.Code))}.";"""),
])

patch("src/system/Modernization.SystemApi/Contracts/SystemContracts.cs", [
    ("""    string PillarKey,
    string RawValueDisplay,
    decimal? MappedScore,""",
     """    string PillarKey,
    Notice Display,
    decimal? MappedScore,"""),
    ("""    bool IsInformational,
    string ExclusionReason,
    string RuleApplied,""",
     """    bool IsInformational,
    Notice? Exclusion,
    string RuleApplied,"""),
    ("""        m.RawValueDisplay,""", """        m.Display,"""),
    ("""        m.ExclusionReason,""", """        m.Exclusion,"""),
    ("""public sealed record RejectionResponse(int RowNumber, string Reason, string? Key);""",
     """public sealed record RejectionResponse(int RowNumber, Notice Reason, string? Key);"""),
    ("""    IReadOnlyList<UploadFileResponse> Files,
    IReadOnlyList<string> Errors)""",
     """    IReadOnlyList<UploadFileResponse> Files,
    IReadOnlyList<Notice> Errors)"""),
    ("""    int RejectedCount,
    string? Error,
""",
     """    int RejectedCount,
    Notice? Error,
"""),
])
ensure_using("src/system/Modernization.SystemApi/Contracts/SystemContracts.cs", "Modernization.Domain")

patch("src/system/Modernization.SystemApi/Contracts/WhatIfContracts.cs", [
    ("""    bool IsAlreadyAchieved,
    string? NotActionableReason)""",
     """    bool IsAlreadyAchieved,
    Notice? NotActionableReason)"""),
])
ensure_using("src/system/Modernization.SystemApi/Contracts/WhatIfContracts.cs", "Modernization.Domain")

patch("src/system/Modernization.SystemApi/Contracts/RoadmapContracts.cs", [
    ("""    string Detail,
    string Evidence);""",
     """    string Detail,
    Notice Evidence);"""),
])
ensure_using("src/system/Modernization.SystemApi/Contracts/RoadmapContracts.cs", "Modernization.Domain")

# ============================================================== capa Channel
convert_problems("src/channel/Modernization.ChannelApi/Endpoints/ChannelEndpoints.cs")

patch("src/channel/Modernization.ChannelApi/Endpoints/ChannelEndpoints.cs", [
    ("""user.FindFirst("name")?.Value ?? user.Identity?.Name ?? "desconocido",""",
     """user.FindFirst("name")?.Value ?? user.Identity?.Name ?? string.Empty,"""),
])

patch("src/channel/Modernization.ChannelApi/SystemApiErrorHandler.cs", [
    ("""        var (status, title, detail) = exception switch
        {
            SystemApiNoDataException noData =>
                (StatusCodes.Status409Conflict, "Sin cargas publicadas", noData.Message),
            SystemApiForbiddenException =>
                (StatusCodes.Status403Forbidden, "Fuera de tu alcance", "Tu usuario no tiene acceso a este recurso del portafolio."),
            _ => (0, string.Empty, string.Empty),
        };""",
     """        var (status, code, title, detail) = exception switch
        {
            SystemApiNoDataException noData =>
                (StatusCodes.Status409Conflict, "no-data", "No published uploads", noData.Message),
            SystemApiForbiddenException =>
                (StatusCodes.Status403Forbidden, "out-of-scope", "Out of scope",
                    "Your user does not have access to this part of the portfolio."),
            _ => (0, string.Empty, string.Empty, string.Empty),
        };"""),
    ("""        await Results.Problem(detail: detail, statusCode: status, title: title)""",
     """        await ApiProblem.Create(status, code, title, detail)"""),
])
ensure_using("src/channel/Modernization.ChannelApi/SystemApiErrorHandler.cs", "Modernization.Security")

patch("src/channel/Modernization.ChannelApi/Program.cs", [
    ("""        await context.Response.WriteAsJsonAsync(new
        {
            title = "Token CSRF ausente o invalido",
            detail = $"Envia el valor de la cookie '{CsrfProtection.CookieName}' en el header '{CsrfProtection.HeaderName}'.",
        }).ConfigureAwait(false);""",
     """        await ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "csrf",
                "Missing or invalid CSRF token",
                $"Send the value of the '{CsrfProtection.CookieName}' cookie in the '{CsrfProtection.HeaderName}' header.")
            .ExecuteAsync(context)
            .ConfigureAwait(false);"""),
])

patch("src/channel/Modernization.ChannelApi/Auth/ChannelAuthentication.cs", [
    ("""new("name", "Usuario de desarrollo"),""", """new("name", "Development user"),"""),
])

patch("src/channel/Modernization.ChannelApi/Clients/SystemApiClient.cs", [
    ('"La capa System no devolvio resultado de la carga."', '"The System layer returned no upload result."'),
    ('"La capa System rechazo la peticion."', '"The System layer rejected the request."'),
    ('$"La capa System devolvio un cuerpo vacio para {url}."', '$"The System layer returned an empty body for {url}."'),
    ('"La capa System no pudo procesar el recurso."', '"The System layer could not process the resource."'),
    (""": base("Todavia no hay ninguna carga publicada. Sube los archivos de las fuentes "
               + "para calcular el portafolio.") { }""",
     """: base(Modernization.Domain.Sources.NoSnapshotAvailableException.DefaultMessage) { }"""),
    (""": base("El recurso esta fuera del alcance del usuario.") { }""", """: base("The resource is outside the user's scope.") { }"""),
])

# ------------------------------------------------------------- view models
patch("src/channel/Modernization.ChannelApi/ViewModels/ScreenViewModels.cs", [
    ("""public sealed record PillarMetricView(
    string MetricKey,
    string Name,
    string RawValueDisplay,""",
     """public sealed record PillarMetricView(
    string MetricKey,
    string Name,
    Notice RawValue,"""),
    ("""    string RulesetVersion,
    IReadOnlyList<string> Steps);""",
     """    string RulesetVersion,
    IReadOnlyList<Notice> Steps);"""),
    ("""public sealed record ExplainMetricView(
    string MetricKey,
    string Name,
    string PillarKey,
    string RawValueDisplay,""",
     """public sealed record ExplainMetricView(
    string MetricKey,
    string Name,
    string PillarKey,
    Notice RawValue,"""),
    ("""    decimal Weight,
    string Reason,
    ProvenanceView Provenance);""",
     """    decimal Weight,
    Notice Reason,
    ProvenanceView Provenance);"""),
    ("""public sealed record LineageRejectionView(int RowNumber, string Reason, string? Key);""",
     """public sealed record LineageRejectionView(int RowNumber, Notice Reason, string? Key);"""),
    ("""    bool IsAlreadyAchieved,
    string? NotActionableReason);""",
     """    bool IsAlreadyAchieved,
    Notice? NotActionableReason);"""),
    ("""    IReadOnlyList<LeverView> IndividualEffects,
    string? InteractionNote);""",
     """    IReadOnlyList<LeverView> IndividualEffects,
    Notice? InteractionNote);"""),
    ("""    ScenarioView? Scenario,
    string Explanation);""",
     """    ScenarioView? Scenario,
    Notice Explanation);"""),
    ("""    int TotalWeeks,
    string Outlook);""",
     """    int TotalWeeks,
    Notice Outlook);"""),
    ("""    string Detail,
    string Evidence);""",
     """    string Detail,
    Notice Evidence);"""),
    ("""    string Provider,
    string? DisabledReason,""",
     """    string Provider,
    Notice? DisabledReason,"""),
    ("""    IReadOnlyList<string> Errors,
    string Summary);""",
     """    IReadOnlyList<Notice> Errors,
    Notice Summary);"""),
    ("""    int RejectedCount,
    string? Error,
""",
     """    int RejectedCount,
    Notice? Error,
"""),
])
ensure_using("src/channel/Modernization.ChannelApi/ViewModels/ScreenViewModels.cs", "Modernization.Domain")

# ------------------------------------------------------------- composers
patch("src/channel/Modernization.ChannelApi/Composition/ScreenComposer.cs", [
    ("""m.MetricKey, m.Name, m.RawValueDisplay, m.MappedScore, m.Weight, m.WeightedScore, m.IsIncluded))]);""",
     """m.MetricKey, m.Name, m.Display, m.MappedScore, m.Weight, m.WeightedScore, m.IsIncluded))]);"""),
    ("""m.MetricKey, m.Name, m.PillarKey, m.Weight, m.ExclusionReason, Provenance(m)))],""",
     """m.MetricKey, m.Name, m.PillarKey, m.Weight, m.Exclusion ?? Notice.Of(NoticeCodes.ValueNoData), Provenance(m)))],"""),
    ("""        m.PillarKey,
        m.RawValueDisplay,
        m.RuleApplied,""",
     """        m.PillarKey,
        m.Display,
        m.RuleApplied,"""),
    ("""            [
                string.Create(CultureInfo.InvariantCulture, $"Numerador: suma de los puntajes ponderados con dato = {score.Numerator:0.###}"),
                string.Create(CultureInfo.InvariantCulture, $"Denominador: suma de los pesos con dato = {score.Denominator:0.##} de 100"),
                string.Create(CultureInfo.InvariantCulture, $"{excluded} metrica(s) sin dato quedan fuera del numerador y del denominador"),
                string.Create(CultureInfo.InvariantCulture, $"Cociente: {score.Numerator:0.###} / {score.Denominator:0.##} = {quotient:0.0000}"),
                string.Create(CultureInfo.InvariantCulture, $"Normalizacion: {ruleset.Floor} + {quotient:0.0000} x {ruleset.Range} = {score.Score:0.00}"),
            ]);""",
     """            [
                // Los pasos son codigos con sus numeros: la interfaz los redacta (ADR 0017).
                Notice.Of(NoticeCodes.FormulaNumerator, ("numerator", score.Numerator)),
                Notice.Of(NoticeCodes.FormulaDenominator, ("denominator", score.Denominator)),
                Notice.Of(NoticeCodes.FormulaExcluded, ("count", excluded)),
                Notice.Of(
                    NoticeCodes.FormulaQuotient,
                    ("numerator", score.Numerator),
                    ("denominator", score.Denominator),
                    ("quotient", Math.Round(quotient, 4, MidpointRounding.AwayFromZero))),
                Notice.Of(
                    NoticeCodes.FormulaNormalization,
                    ("floor", ruleset.Floor),
                    ("quotient", Math.Round(quotient, 4, MidpointRounding.AwayFromZero)),
                    ("range", ruleset.Range),
                    ("score", score.Score)),
            ]);"""),
    ("""first?.Provider ?? "desconocido",""", """first?.Provider ?? string.Empty,"""),
    ("""version?.Label ?? "sin carga");""", """version?.Label ?? string.Empty);"""),
    (""".GroupBy(a => selector(a) ?? "Sin asignar", StringComparer.Ordinal)""",
     """.GroupBy(a => selector(a) ?? string.Empty, StringComparer.Ordinal)"""),
    ("""return new ScoreBandView(b.Name, b.Range, count, percent);""",
     """return new ScoreBandView(b.Code, b.Range, count, percent);"""),
])

replace_between(
    "src/channel/Modernization.ChannelApi/Composition/RoadmapComposer.cs",
    "    private static string Outlook(RoadmapPlanResponse plan)",
    """en unas {plan.TotalWeeks} semanas.");
    }""",
    """    private static Notice Outlook(RoadmapPlanResponse plan)
    {
        if (plan.Roadmap is null)
        {
            return Notice.Of(NoticeCodes.OutlookNoRoadmap);
        }

        // Retire y Consolidate no proyectan mejora: su score deja de existir.
        if (plan.LeavesPortfolio)
        {
            return Notice.Of(NoticeCodes.OutlookLeavesPortfolio, ("roadmap", plan.Roadmap), ("weeks", plan.TotalWeeks));
        }

        if (plan.ProjectedScore is not { } projected)
        {
            return Notice.Of(NoticeCodes.OutlookNoScoreEffect, ("roadmap", plan.Roadmap));
        }

        return Notice.Of(
            NoticeCodes.OutlookProjected,
            ("roadmap", plan.Roadmap),
            ("baseline", plan.BaselineScore),
            ("projected", projected),
            ("delta", plan.ProjectedDelta),
            ("weeks", plan.TotalWeeks));
    }""")
ensure_using("src/channel/Modernization.ChannelApi/Composition/RoadmapComposer.cs", "Modernization.Domain")

replace_between(
    "src/channel/Modernization.ChannelApi/Composition/WhatIfComposer.cs",
    "    private static string? InteractionNote(ScenarioResponse r)",
    """a {r.Result?.Projected.EffectiveScore:0.00}.";
    }""",
    """    private static Notice? InteractionNote(ScenarioResponse r)
    {
        if (r.InteractionEffect == 0m)
        {
            return null;
        }

        var added = r.Projected.Denominator - r.Baseline.Denominator;

        return added > 0
            ? Notice.Of(
                NoticeCodes.ScenarioWeightAdded,
                ("from", r.Baseline.Denominator),
                ("to", r.Projected.Denominator),
                ("sum", r.SumOfIndividualDeltas),
                ("delta", r.Delta))
            : Notice.Of(NoticeCodes.ScenarioInteraction, ("effect", r.InteractionEffect));
    }

    private static Notice Explain(TargetResponse r)
    {
        if (r.IsAlreadyMet)
        {
            return Notice.Of(NoticeCodes.TargetAlreadyMet, ("score", r.BaselineScore));
        }

        if (!r.IsReachable)
        {
            return Notice.Of(NoticeCodes.TargetUnreachable, ("target", r.Target), ("ceiling", r.CeilingScore));
        }

        return Notice.Of(
            NoticeCodes.TargetReachable,
            ("count", r.Result?.Applied.Count ?? 0),
            ("baseline", r.BaselineScore),
            ("projected", r.Result?.Projected.EffectiveScore));
    }""")
ensure_using("src/channel/Modernization.ChannelApi/Composition/WhatIfComposer.cs", "Modernization.Domain")

patch("src/channel/Modernization.ChannelApi/Composition/IngestionComposer.cs", [
    ("""        string? disabledReason = null;

        if (!capabilities.UploadEnabled)
        {
            disabledReason = $"El adaptador activo es '{provider}', que sirve datos generados. "
                + "La carga de archivos requiere el adaptador de archivos.";
        }
        else if (!canUpload)
        {
            disabledReason = "Tu usuario puede consultar las cargas pero no publicarlas. Publicar requiere "
                + "el rol 'ingestion.write' y alcance sobre todo el portafolio.";
        }""",
     """        Notice? disabledReason = null;

        if (!capabilities.UploadEnabled)
        {
            disabledReason = Notice.Of(NoticeCodes.UploadDisabledAdapter, ("provider", provider));
        }
        else if (!canUpload)
        {
            disabledReason = Notice.Of(NoticeCodes.UploadDisabledPermission, ("role", ChannelPermissions.UploadRole));
        }"""),
])
replace_between(
    "src/channel/Modernization.ChannelApi/Composition/IngestionComposer.cs",
    "    private static string Summarize(UploadResponse outcome)",
    """revisa el detalle por archivo.";
    }""",
    """    private static Notice Summarize(UploadResponse outcome)
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
    }""")
ensure_using("src/channel/Modernization.ChannelApi/Composition/IngestionComposer.cs", "Modernization.Domain")
