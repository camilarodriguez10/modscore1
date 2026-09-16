using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Modernization.ChannelApi.Clients;
using Modernization.Security;

namespace Modernization.Api.Tests;

/// <summary>
/// Fase 15 (ADR 0020, opcion C): el token entre capas puede ser de vida corta.
/// </summary>
/// <remarks>
/// Estas pruebas cubren las dos mitades sin APIM: la capa System valida un JWT
/// firmado, y la capa Channel lo pide con client_credentials, lo reutiliza y lo
/// renueva. Lo que queda para un entorno real es el emisor.
/// </remarks>
public sealed class JwtServiceTokenValidatorTests : IDisposable
{
    private const string Issuer = "https://apim.example.com/modscore";
    private const string Audience = "api://modernization-system";

    private readonly RSA _rsa = RSA.Create(2048);
    private readonly RSA _other = RSA.Create(2048);

    public void Dispose()
    {
        _rsa.Dispose();
        _other.Dispose();
    }

    [Fact]
    public async Task AValidTokenIsAcceptedAndSaysWhoCalled()
    {
        var validator = Validator();
        var token = Token();

        var outcome = await validator.ValidateAsync(token);

        Assert.True(outcome.IsValid);
        Assert.Equal("channel-api", outcome.Caller);
    }

    [Fact]
    public async Task ATokenForAnotherAudienceIsRejected()
    {
        var validator = Validator();
        var token = Token(audience: "api://otra-cosa");

        var outcome = await validator.ValidateAsync(token);

        Assert.False(outcome.IsValid);
    }

    [Fact]
    public async Task ATokenFromAnotherIssuerIsRejected()
    {
        var validator = Validator();
        var token = Token(issuer: "https://otro-emisor.example.com");

        var outcome = await validator.ValidateAsync(token);

        Assert.False(outcome.IsValid);
    }

    [Fact]
    public async Task AnExpiredTokenIsRejected()
    {
        var validator = Validator();
        var token = Token(expires: DateTime.UtcNow.AddHours(-1));

        var outcome = await validator.ValidateAsync(token);

        Assert.False(outcome.IsValid);
    }

    /// <summary>Firmar con otra llave es exactamente lo que haria quien fabrica un token.</summary>
    [Fact]
    public async Task ATokenSignedWithAnotherKeyIsRejected()
    {
        var validator = Validator();
        var token = Token(key: new RsaSecurityKey(_other) { KeyId = "otra" });

        var outcome = await validator.ValidateAsync(token);

        Assert.False(outcome.IsValid);
    }

    [Fact]
    public async Task AnUnsignedTokenIsRejected()
    {
        var validator = Validator();

        var unsigned = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
        });

        var outcome = await validator.ValidateAsync(unsigned);

        Assert.False(outcome.IsValid);
    }

    [Fact]
    public async Task ACallerOutsideTheListIsRejected()
    {
        var validator = Validator("channel-api");
        var token = Token(caller: "otro-cliente");

        var outcome = await validator.ValidateAsync(token);

        Assert.False(outcome.IsValid);
    }

    // -----------------------------------------------------------------------

    private JwtServiceTokenValidator Validator(params string[] allowedCallers)
    {
        var options = new JwtServiceTokenOptions { Issuer = Issuer, Audience = Audience };
        options.SigningKeys.Add(new RsaSecurityKey(_rsa) { KeyId = "vigente" });

        foreach (var caller in allowedCallers)
        {
            options.AllowedCallers.Add(caller);
        }

        return new JwtServiceTokenValidator(options, NullLogger<JwtServiceTokenValidator>.Instance);
    }

    private string Token(
        string issuer = Issuer,
        string audience = Audience,
        string caller = "channel-api",
        DateTime? expires = null,
        SecurityKey? key = null)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["azp"] = caller },
            SigningCredentials = new SigningCredentials(
                key ?? new RsaSecurityKey(_rsa) { KeyId = "vigente" },
                SecurityAlgorithms.RsaSha256),
        });
}

/// <summary>
/// La credencial de la capa Channel: se pide una vez, se reutiliza mientras sirve y
/// se renueva antes de que venza.
/// </summary>
public sealed class ClientCredentialsServiceCredentialTests
{
    private static ClientCredentialsOptions Options() => new()
    {
        TokenEndpoint = "https://apim.example.com/oauth2/token",
        ClientId = "channel-api",
        ClientSecret = "un-secreto-de-prueba-suficientemente-largo",
        Scope = "api://modernization-system/.default",
        RenewBefore = TimeSpan.FromSeconds(60),
    };

    [Fact]
    public async Task TheTokenIsReusedUntilItIsAboutToExpire()
    {
        var issuer = new TokenIssuer(expiresIn: 300);
        var clock = new TestClock(DateTimeOffset.Parse("2026-09-16T10:00:00Z", null));
        var credential = new ClientCredentialsServiceCredential(new SingleClientFactory(issuer), Options(), clock);

        var first = await credential.GetAsync();
        var second = await credential.GetAsync();

        Assert.Equal(first, second);
        Assert.Equal(1, issuer.Requests);

        // A falta de un minuto para vencer, se pide uno nuevo.
        clock.Advance(TimeSpan.FromSeconds(241));
        var third = await credential.GetAsync();

        Assert.NotEqual(first, third);
        Assert.Equal(2, issuer.Requests);
    }

    [Fact]
    public async Task TwentySimultaneousCallersShareOneRequest()
    {
        var issuer = new TokenIssuer(expiresIn: 300, delay: TimeSpan.FromMilliseconds(50));
        var clock = new TestClock(DateTimeOffset.Parse("2026-09-16T10:00:00Z", null));
        var credential = new ClientCredentialsServiceCredential(new SingleClientFactory(issuer), Options(), clock);

        var tokens = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => credential.GetAsync().AsTask()));

        Assert.Single(tokens.Distinct(StringComparer.Ordinal));
        Assert.Equal(1, issuer.Requests);
    }

    [Fact]
    public async Task AnErrorFromTheIssuerDoesNotLeakItsBody()
    {
        var issuer = new TokenIssuer(status: HttpStatusCode.Unauthorized, body: "{\"error\":\"invalid_client\",\"secreto\":\"no-repetir\"}");
        var clock = new TestClock(DateTimeOffset.Parse("2026-09-16T10:00:00Z", null));
        var credential = new ClientCredentialsServiceCredential(new SingleClientFactory(issuer), Options(), clock);

        var error = await Assert.ThrowsAsync<ServiceCredentialException>(async () => await credential.GetAsync());

        Assert.Contains("401", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no-repetir", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEndpointThatIsNotHttpsIsRejectedAtStartup()
    {
        var options = Options();
        options.TokenEndpoint = "http://apim.example.com/oauth2/token";

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    // -----------------------------------------------------------------------

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class TokenIssuer(
        int expiresIn = 300,
        TimeSpan? delay = null,
        HttpStatusCode status = HttpStatusCode.OK,
        string? body = null) : HttpMessageHandler
    {
        private int _requests;

        public int Requests => Volatile.Read(ref _requests);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var number = Interlocked.Increment(ref _requests);

            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            var content = body ?? $"{{\"access_token\":\"token-{number}\",\"expires_in\":{expiresIn}}}";

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

/// <summary>
/// La ventana del cambio a APIM: mientras dura, la capa System acepta el secreto
/// compartido y el token de vida corta. Sin eso, el cambio pediria un corte.
/// </summary>
public sealed class EitherServiceTokenValidatorTests : IDisposable
{
    private const string Shared = "secreto-compartido-de-prueba-con-largo-suficiente";
    private const string Issuer = "https://apim.example.com/modscore";
    private const string Audience = "api://modernization-system";

    private readonly RSA _rsa = RSA.Create(2048);

    public void Dispose() => _rsa.Dispose();

    [Fact]
    public async Task DuringTheChangeTheSharedSecretStillWorks()
    {
        var outcome = await Validator().ValidateAsync(Shared);

        Assert.True(outcome.IsValid);
        Assert.Equal("channel-api", outcome.Caller);
    }

    [Fact]
    public async Task DuringTheChangeTheShortLivedTokenAlsoWorks()
    {
        var outcome = await Validator().ValidateAsync(Token());

        Assert.True(outcome.IsValid);
        Assert.Equal("channel-api-apim", outcome.Caller);
    }

    [Fact]
    public async Task AnythingElseIsStillRejected()
    {
        var outcome = await Validator().ValidateAsync("ni-el-secreto-ni-un-token-valido");

        Assert.False(outcome.IsValid);
    }

    // -----------------------------------------------------------------------

    private EitherServiceTokenValidator Validator()
    {
        var shared = new ServiceTokenOptions
        {
            Mode = ServiceTokenMode.Both,
            Token = Shared,
            CallerName = "channel-api",
        };

        var issued = new JwtServiceTokenOptions { Issuer = Issuer, Audience = Audience };
        issued.SigningKeys.Add(new RsaSecurityKey(_rsa) { KeyId = "vigente" });

        return new EitherServiceTokenValidator(
            new StaticServiceTokenValidator(new SingleOptions(shared)),
            new JwtServiceTokenValidator(issued, NullLogger<JwtServiceTokenValidator>.Instance));
    }

    private string Token()
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object>(StringComparer.Ordinal) { ["azp"] = "channel-api-apim" },
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(_rsa) { KeyId = "vigente" },
                SecurityAlgorithms.RsaSha256),
        });

    private sealed class SingleOptions(ServiceTokenOptions options) : IOptionsMonitor<ServiceTokenOptions>
    {
        public ServiceTokenOptions CurrentValue => options;

        public ServiceTokenOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<ServiceTokenOptions, string?> listener) => null;
    }
}
