"""Fase 15 - opcion C: la capa System acepta un token de vida corta ademas del secreto."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if old not in text:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:160])
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


# --------------------------------------------- el validador JWT: partial y Dispose
patch("src/shared/Modernization.Security/JwtServiceTokenValidator.cs", [
    (
        "public sealed class JwtServiceTokenValidator : IServiceTokenValidator, IDisposable",
        "public sealed partial class JwtServiceTokenValidator : IServiceTokenValidator, IDisposable",
    ),
    (
        "    public void Dispose() => _handler.Dispose();",
        "    public void Dispose() => (_discovery as IDisposable)?.Dispose();",
    ),
])

# ------------------------------------------------- el handler usa el validador
patch("src/shared/Modernization.Security/ServiceTokenAuthentication.cs", [
    (
        """using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;""",
        """using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;""",
    ),
    (
        """    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var value = header.ToString();
        const string Prefix = "Bearer ";

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Se esperaba un token Bearer."));
        }

        var presented = value[Prefix.Length..];

        if (!IsExpectedToken(presented))
        {
            // Sin detalle: un mensaje especifico ayudaria a enumerar tokens.
            return Task.FromResult(AuthenticateResult.Fail("Token de servicio invalido."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, Options.CallerName), new Claim(CallerTypeClaim, ServiceCallerType)],
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    /// <summary>
    /// Acepta el token vigente y, durante una rotacion, el siguiente. Asi se cambia
    /// el secreto sin cortar el servicio (runbook de rotacion de secretos).
    /// </summary>
    private bool IsExpectedToken(string presented)
    {
        var actual = Encoding.UTF8.GetBytes(presented);

        var primary = CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Options.Token), actual);
        var secondary = !string.IsNullOrEmpty(Options.SecondaryToken)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Options.SecondaryToken), actual);

        return primary || secondary;
    }
}""",
        """    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header))
        {
            return AuthenticateResult.NoResult();
        }

        var value = header.ToString();
        const string Prefix = "Bearer ";

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Se esperaba un token Bearer.");
        }

        var presented = value[Prefix.Length..];

        // Que token es valido lo decide el validador configurado: secreto compartido
        // hoy, token de vida corta de APIM cuando exista ([ADR 0020], opcion C).
        var validator = Context.RequestServices.GetRequiredService<IServiceTokenValidator>();
        var outcome = await validator.ValidateAsync(presented, Context.RequestAborted).ConfigureAwait(false);

        if (!outcome.IsValid)
        {
            // Sin detalle en la respuesta: un mensaje especifico ayudaria a enumerar tokens.
            return AuthenticateResult.Fail("Token de servicio invalido.");
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.Name, outcome.Caller ?? Options.CallerName),
                new Claim(CallerTypeClaim, ServiceCallerType),
            ],
            SchemeName);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}""",
    ),
    (
        """public sealed class ServiceTokenOptions : AuthenticationSchemeOptions
{
    private const int MinimumLength = 32;""",
        """public sealed class ServiceTokenOptions : AuthenticationSchemeOptions
{
    private const int MinimumLength = 32;

    /// <summary>
    /// Como se valida lo que presenta la capa Channel. <see cref="ServiceTokenMode.Static"/>
    /// exige el secreto compartido; <see cref="ServiceTokenMode.Jwt"/> no lo usa.
    /// </summary>
    public ServiceTokenMode Mode { get; set; } = ServiceTokenMode.Static;""",
    ),
    (
        """    public override void Validate()
    {
        base.Validate();

        if (string.IsNullOrWhiteSpace(Token) || Token.Length < MinimumLength)""",
        """    public override void Validate()
    {
        base.Validate();

        if (Mode == ServiceTokenMode.Jwt)
        {
            // Con APIM el secreto compartido no existe: lo que hay que validar son
            // el emisor, la audiencia y las llaves, y de eso se ocupa su validador.
            return;
        }

        if (string.IsNullOrWhiteSpace(Token) || Token.Length < MinimumLength)""",
    ),
])
