"""Fase 14 · SEC-06 (ASVS 7.1.3): auditar las entradas rechazadas por validacion."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old = old.replace("\n", "\r\n")
            new = new.replace("\n", "\r\n")
        if new in text:
            raise SystemExit(f"ya aplicado: {rel}")
        if old not in text:
            raise SystemExit(f"ancla no encontrada en {rel}:\n{old[:120]}")
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


def write(rel, body):
    path = root / rel
    path.write_text(body.replace("\n", "\r\n"), encoding="utf-8", newline="")
    print("escrito", rel)


# ------------------------------------------------------ evento de auditoria
patch("src/shared/Modernization.Security/SecurityAudit.cs", [
    (
        """    public static void StepUpCompleted(ILogger logger, string? user)""",
        """    /// <summary>
    /// Una entrada rechazada por validacion (ASVS 7.1.3). Se registran la ruta y el codigo,
    /// nunca el cuerpo ni los valores enviados: el log no es lugar para datos sin validar.
    /// </summary>
    public static void ValidationRejected(ILogger logger, string? user, string? path, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanUser = Clean(user);
            var cleanPath = Clean(path);
            LogValidationRejected(logger, cleanUser, cleanPath, statusCode);
        }
    }

    /// <summary>
    /// Codigos con los que el borde dice "esta entrada no paso la validacion": 400 (invalida),
    /// 413 (demasiado grande), 415 (tipo no aceptado) y 422 (semanticamente invalida).
    /// </summary>
    public static bool IsValidationRejection(int statusCode)
        => statusCode is 400 or 413 or 415 or 422;

    public static void StepUpCompleted(ILogger logger, string? user)""",
    ),
    (
        """    [LoggerMessage(EventId = 4212, Level = LogLevel.Warning,
        Message = "Publicacion de reglas rechazada. usuario={User} motivo={Reason}")]
    private static partial void LogRulesRejected(ILogger logger, string user, string reason);""",
        """    [LoggerMessage(EventId = 4212, Level = LogLevel.Warning,
        Message = "Publicacion de reglas rechazada. usuario={User} motivo={Reason}")]
    private static partial void LogRulesRejected(ILogger logger, string user, string reason);

    [LoggerMessage(EventId = 4214, Level = LogLevel.Warning,
        Message = "Entrada rechazada por validacion. usuario={User} ruta={Path} codigo={StatusCode}")]
    private static partial void LogValidationRejected(ILogger logger, string user, string path, int statusCode);""",
    ),
])

# ------------------------------------------------- middleware de la Channel
patch("src/channel/Modernization.ChannelApi/Program.cs", [
    (
        """    await next().ConfigureAwait(false);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}""",
        """    await next().ConfigureAwait(false);
});

// Una entrada rechazada por validacion tambien es informacion de seguridad (ASVS 7.1.3).
// Se audita aqui, y no endpoint por endpoint, para que ninguno se olvide: cualquier
// respuesta de /api que rechace la entrada deja el evento 4214 con su ruta y su codigo.
app.Use(async (context, next) =>
{
    await next().ConfigureAwait(false);

    if (SecurityAudit.IsValidationRejection(context.Response.StatusCode)
        && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
    {
        SecurityAudit.ValidationRejected(
            audit,
            ChannelPermissions.SubjectOf(context.User),
            context.Request.Path.Value,
            context.Response.StatusCode);
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}""",
    ),
])

# ------------------------------------------------------------------ pruebas
write("tests/Modernization.Api.Tests/ValidationAuditTests.cs", '''using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Modernization.Api.Tests;

/// <summary>
/// ASVS 7.1.3 (SEC-06): una entrada rechazada por validacion deja rastro. El middleware de
/// la capa Channel audita el evento 4214 con la ruta y el codigo, y sin los valores que
/// mando quien llamo: el log no repite datos sin validar.
/// </summary>
public sealed class ValidationAuditTests
{
    private static Uri Api(string path) => new(path, UriKind.Relative);

    private static (AuditLog Audit, TwoLayerHost Host) Audited()
    {
        var audit = new AuditLog();
        return (audit, RulesHosts.Create(channelServices: services => services.AddSingleton<ILoggerProvider>(audit)));
    }

    [Fact]
    public async Task ARejectedFilterIsAuditedWithoutTheValueThatWasSent()
    {
        var (audit, host) = Audited();
        using var _ = host;
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.GetAsync(Api("/api/portfolio?band=nunca-existio"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var entry = Assert.Single(audit.Entries, e => e.EventId == 4214);
        Assert.Contains("/api/portfolio", entry.Message, StringComparison.Ordinal);
        Assert.Contains("400", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("nunca-existio", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AValidRequestIsNotAudited()
    {
        var (audit, host) = Audited();
        using var _ = host;
        var client = await host.CreateMutatingClientAsync();

        using var response = await client.GetAsync(Api("/api/portfolio"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(audit.Entries, e => e.EventId == 4214);
    }

    [Fact]
    public async Task AMalformedRulesDraftIsAudited()
    {
        var (audit, host) = Audited();
        using var _ = host;
        var client = await host.CreateMutatingClientAsync();

        using var content = new StringContent("{ esto no es json", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(Api("/api/admin/rules/simulate"), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(
            audit.Entries,
            e => e.EventId == 4214 && e.Message.Contains("/api/admin/rules/simulate", StringComparison.Ordinal));
    }
}
''')
