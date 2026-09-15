import io, os
root = r"C:\Users\kmiro\Downloads\mod score"

def edit(rel, pairs):
    p = os.path.join(root, rel)
    s = io.open(p, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {rel}: {old[:70]!r}"
        s = s.replace(old, new)
    io.open(p, 'w', encoding='utf-8').write(s)
    print("ok", rel)

# 1. CSRF: hex en lugar de Base64 (ASP.NET codifica en URL el valor de la cookie)
edit(r"src\channel\Modernization.ChannelApi\Auth\CsrfProtection.cs", [(
    "        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));",
    "        // Hexadecimal, no Base64: ASP.NET Core codifica en URL el valor de la\n"
    "        // cookie al escribirla, asi que un valor con '+', '/' o '=' no coincidiria\n"
    "        // con lo que el cliente lee de document.cookie y reenvia en el header.\n"
    "        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));"
)])

# 2. Channel: validacion de EAI y errores que no hacen eco
edit(r"src\channel\Modernization.ChannelApi\Endpoints\ChannelEndpoints.cs", [
    ("using Modernization.ChannelApi.ViewModels;",
     "using Modernization.ChannelApi.ViewModels;\nusing Modernization.Security;"),

    ("        var view = await composer.ComposeOverviewAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "        return view is null ? NotFound(eaiCode) : Results.Ok(view);",
     "        if (!InputValidation.IsValidEaiCode(eaiCode))\n"
     "        {\n            return InvalidEaiCode();\n        }\n\n"
     "        var view = await composer.ComposeOverviewAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "        return view is null ? NotFound() : Results.Ok(view);"),

    ("        var view = await composer.ComposeExplainAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "        return view is null ? NotFound(eaiCode) : Results.Ok(view);",
     "        if (!InputValidation.IsValidEaiCode(eaiCode))\n"
     "        {\n            return InvalidEaiCode();\n        }\n\n"
     "        var view = await composer.ComposeExplainAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "        return view is null ? NotFound() : Results.Ok(view);"),

    ("    private static IResult NotFound(string eaiCode) => Results.Problem(\n"
     "        detail: $\"No existe una aplicacion con EAI '{eaiCode}'.\",\n"
     "        statusCode: StatusCodes.Status404NotFound,\n"
     "        title: \"Aplicacion no encontrada\");",
     "    /// <summary>\n"
     "    /// No se hace eco del valor recibido. El cliente ya sabe que pidio, y devolver\n"
     "    /// entrada del usuario dentro de un mensaje de error es como nacen los XSS\n"
     "    /// reflejados el dia que alguien renderiza ese texto como HTML.\n"
     "    /// </summary>\n"
     "    private static IResult NotFound() => Results.Problem(\n"
     "        detail: \"No existe una aplicacion con ese EAI en el portafolio cargado.\",\n"
     "        statusCode: StatusCodes.Status404NotFound,\n"
     "        title: \"Aplicacion no encontrada\");\n\n"
     "    private static IResult InvalidEaiCode() => Results.Problem(\n"
     "        detail: \"Un EAI code es una secuencia de hasta 10 digitos.\",\n"
     "        statusCode: StatusCodes.Status400BadRequest,\n"
     "        title: \"EAI code invalido\");"),
])

# 3. System: misma validacion, defensa en profundidad
edit(r"src\system\Modernization.SystemApi\Endpoints\SystemEndpoints.cs", [
    ("using Modernization.Scoring.Engine;",
     "using Modernization.Scoring.Engine;\nusing Modernization.Security;"),

    ("        var application = await portfolio.FindAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "        return application is null\n            ? NotFound(eaiCode)\n            : Results.Ok(ApplicationResponse.From(application));",
     "        if (!InputValidation.IsValidEaiCode(eaiCode))\n"
     "        {\n            return InvalidEaiCode();\n        }\n\n"
     "        var application = await portfolio.FindAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "        return application is null\n            ? NotFound()\n            : Results.Ok(ApplicationResponse.From(application));"),

    ("        try\n        {\n            var score = await portfolio.GetScoreAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "            return score is null\n                ? NotFound(eaiCode)\n                : Results.Ok(ScoreResponse.From(score));\n        }",
     "        if (!InputValidation.IsValidEaiCode(eaiCode))\n"
     "        {\n            return InvalidEaiCode();\n        }\n\n"
     "        try\n        {\n            var score = await portfolio.GetScoreAsync(eaiCode, cancellationToken).ConfigureAwait(false);\n\n"
     "            return score is null\n                ? NotFound()\n                : Results.Ok(ScoreResponse.From(score));\n        }"),

    ("    /// <summary>\n"
     "    /// El EAI se hace eco en el detalle. Se codifica al renderizar, y el cliente\n"
     "    /// que lo consume es la capa Channel, no un navegador.\n"
     "    /// </summary>\n"
     "    private static IResult NotFound(string eaiCode) => Results.Problem(\n"
     "        detail: $\"No existe una aplicacion con EAI '{eaiCode}' en el portafolio cargado.\",\n"
     "        statusCode: StatusCodes.Status404NotFound,\n"
     "        title: \"Aplicacion no encontrada\");",
     "    private static IResult NotFound() => Results.Problem(\n"
     "        detail: \"No existe una aplicacion con ese EAI en el portafolio cargado.\",\n"
     "        statusCode: StatusCodes.Status404NotFound,\n"
     "        title: \"Aplicacion no encontrada\");\n\n"
     "    private static IResult InvalidEaiCode() => Results.Problem(\n"
     "        detail: \"Un EAI code es una secuencia de hasta 10 digitos.\",\n"
     "        statusCode: StatusCodes.Status400BadRequest,\n"
     "        title: \"EAI code invalido\");"),
])

# 4. Tests: el caso malicioso ahora se rechaza en la puerta
edit(r"tests\Modernization.Api.Tests\SecurityTests.cs", [(
    "    /// <summary>\n"
    "    /// Un EAI con caracteres de control no puede llegar crudo a la URL de la capa\n"
    "    /// System ni volver sin codificar en la respuesta.\n"
    "    /// </summary>\n"
    "    [Fact]\n"
    "    public async Task MaliciousEaiCodeIsHandledSafely()\n"
    "    {\n"
    "        var client = await fixture.CreateAuthenticatedChannelClientAsync();\n\n"
    "        using var response = await client.GetAsync(\n"
    "            new Uri(\"/api/applications/%3Cscript%3Ealert(1)%3C%2Fscript%3E/overview\", UriKind.Relative));\n\n"
    "        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);\n\n"
    "        var body = await response.Content.ReadAsStringAsync();\n"
    "        Assert.DoesNotContain(\"<script>\", body, StringComparison.Ordinal);\n"
    "    }",

    "    /// <summary>\n"
    "    /// Un EAI que no cumple el formato se rechaza en la puerta: no llega al\n"
    "    /// compositor, ni a la URL de la capa System, ni de vuelta en la respuesta.\n"
    "    /// </summary>\n"
    "    [Theory]\n"
    "    [InlineData(\"%3Cscript%3Ealert(1)%3C%2Fscript%3E\")]\n"
    "    [InlineData(\"..%2F..%2Fetc%2Fpasswd\")]\n"
    "    [InlineData(\"14381%27%20OR%201%3D1\")]\n"
    "    [InlineData(\"99999999999999999999\")]\n"
    "    public async Task MalformedEaiCodeIsRejectedAtTheEdge(string eaiCode)\n"
    "    {\n"
    "        var client = await fixture.CreateAuthenticatedChannelClientAsync();\n\n"
    "        using var response = await client.GetAsync(\n"
    "            new Uri($\"/api/applications/{eaiCode}/overview\", UriKind.Relative));\n\n"
    "        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);\n"
    "    }\n\n"
    "    /// <summary>El mensaje de error no hace eco de la entrada del usuario.</summary>\n"
    "    [Fact]\n"
    "    public async Task ErrorMessagesDoNotEchoUserInput()\n"
    "    {\n"
    "        var client = await fixture.CreateAuthenticatedChannelClientAsync();\n\n"
    "        using var response = await client.GetAsync(\n"
    "            new Uri(\"/api/applications/%3Cscript%3E/overview\", UriKind.Relative));\n\n"
    "        var body = await response.Content.ReadAsStringAsync();\n\n"
    "        Assert.DoesNotContain(\"script\", body, StringComparison.OrdinalIgnoreCase);\n"
    "    }"
)])
