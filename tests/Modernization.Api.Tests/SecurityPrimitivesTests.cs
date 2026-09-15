using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Modernization.Domain.Sources;
using Modernization.Scoring.Engine;
using Modernization.Security;
using Modernization.Sources.Dummy;
using Modernization.SystemApi.Services;

namespace Modernization.Api.Tests;

public sealed class PortfolioScopeTests
{
    [Fact]
    public void FullScopeAllowsEverything()
    {
        Assert.True(PortfolioScope.Full.Allows(null, null, null));
        Assert.True(PortfolioScope.FromHeader("*").IsFull);
    }

    /// <summary>Por defecto no se ve nada: la ausencia de alcance nunca es el portafolio completo.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AMissingScopeGrantsNothing(string? header)
    {
        var scope = PortfolioScope.FromHeader(header);

        Assert.True(scope.IsEmpty);
        Assert.False(scope.Allows("Individual", "Chile", "Ana Perez"));
    }

    [Fact]
    public void AScopeIsTheUnionOfItsEntries()
    {
        var scope = PortfolioScope.FromHeader("lob:Individual;country:Chile");

        Assert.True(scope.Allows("Individual", "Mexico", null));
        Assert.True(scope.Allows("Group Benefits", "Chile", null));
        Assert.False(scope.Allows("Group Benefits", "Mexico", null));
    }

    [Theory]
    [InlineData("lob")]
    [InlineData(":Individual")]
    [InlineData("region:LATAM")]
    [InlineData("lob:")]
    [InlineData("**")]
    public void MalformedEntriesNeverWidenTheScope(string header)
        => Assert.True(PortfolioScope.FromHeader(header).IsEmpty);

    [Fact]
    public void ComparisonIgnoresCase()
        => Assert.True(PortfolioScope.FromHeader("lob:individual").Allows("Individual", null, null));

    /// <summary>Un valor con ';' o '&amp;' viaja escapado y vuelve intacto.</summary>
    [Fact]
    public void HeaderRoundTripsValuesWithSeparators()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("portfolio_scope", "lob:Retirement & Income; Solutions")],
            "tests"));

        var restored = PortfolioScope.FromHeader(PortfolioScope.FromClaims(user, "portfolio_scope").ToHeaderValue());

        Assert.True(restored.Allows("Retirement & Income; Solutions", null, null));
        Assert.False(restored.Allows("Retirement & Income", null, null));
    }

    [Fact]
    public void ControlCharactersInAValueInvalidateTheEntry()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("portfolio_scope", "lob:Indi\nvidual")],
            "tests"));

        Assert.True(PortfolioScope.FromClaims(user, "portfolio_scope").IsEmpty);
    }

    /// <summary>Un header con miles de entradas no es un alcance: lo que excede el tope se ignora.</summary>
    [Fact]
    public void EntriesBeyondTheCapAreIgnored()
    {
        var header = string.Join(';', Enumerable.Range(0, 64).Select(i => $"lob:L{i}")) + ";*";

        Assert.False(PortfolioScope.FromHeader(header).IsFull);
    }

    [Fact]
    public async Task OnlyAServiceCallerCanDeclareAScope()
    {
        var anonymous = new DefaultHttpContext();
        anonymous.Request.Headers[PortfolioScope.HeaderName] = "*";

        var service = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ServiceTokenAuthenticationHandler.CallerTypeClaim, ServiceTokenAuthenticationHandler.ServiceCallerType)],
                ServiceTokenAuthenticationHandler.SchemeName)),
        };
        service.Request.Headers[PortfolioScope.HeaderName] = "*";

        Assert.True((await PortfolioScope.BindAsync(anonymous))!.IsEmpty);
        Assert.True((await PortfolioScope.BindAsync(service))!.IsFull);
    }
}

public sealed class UploadLabelTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  Carga 0809  ", "Carga 0809")]
    [InlineData("Revisión de María — Q3", "Revisión de María — Q3")]
    public void ValidLabelsAreNormalized(string? raw, string? expected)
    {
        Assert.True(UploadLabel.TryNormalize(raw, out var label, out var error));
        Assert.Equal(expected, label);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("linea\nnueva")]
    [InlineData("tab\tinterno")]
    [InlineData("orden‮invertido")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public void UnsafeLabelsAreRejectedWithAReason(string raw)
    {
        Assert.False(UploadLabel.TryNormalize(raw, out var label, out var error));
        Assert.Null(label);
        Assert.NotNull(error);
    }
}

public sealed class AuditValueTests
{
    /// <summary>Un valor externo no puede partir una linea de log ni invertir su lectura.</summary>
    [Fact]
    public void CleanNeutralizesLineBreaksAndFormatCharacters()
        => Assert.Equal("a??b?c", SecurityAudit.Clean("a\r\nb‮c"));

    [Fact]
    public void CleanBoundsTheLength()
        => Assert.Equal(201, SecurityAudit.Clean(new string('x', 500)).Length);

    [Theory]
    [InlineData("dev-user", "dev-user")]
    [InlineData("00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
    [InlineData("usuario\ncon salto", "desconocido")]
    [InlineData("<script>", "desconocido")]
    [InlineData("", "desconocido")]
    public void ForwardedSubjectsAreWhitelisted(string raw, string expected)
        => Assert.Equal(expected, ForwardedUser.Sanitize(raw));
}

/// <summary>
/// Consistencia del servicio de portafolio bajo concurrencia y costo de la historia.
/// </summary>
public sealed class PortfolioServiceConsistencyTests
{
    private static readonly RulesService Rules = new(new Modernization.Domain.Rules.InMemoryRulesStore());

    /// <summary>
    /// Una carga publicada mientras otra peticion leia el portafolio: la lectura
    /// vieja no puede quedar fijada como vigente.
    /// </summary>
    [Fact]
    public async Task AnUploadDuringALoadIsNotOverwrittenByTheStaleRead()
    {
        var provider = new GatedProvider();
        using var service = new PortfolioService(provider, Rules);

        var loading = service.GetSnapshotAsync(PortfolioScope.Full);
        await provider.FirstReadStarted.Task;

        provider.Publish();
        service.Invalidate();
        provider.ReleaseFirstRead();

        var snapshot = await loading;

        Assert.Equal(GatedProvider.PublishedCount, snapshot.Applications.Count);
        Assert.Equal(
            GatedProvider.PublishedCount,
            (await service.GetSnapshotAsync(PortfolioScope.Full)).Applications.Count);
    }

    /// <summary>
    /// Una version publicada no cambia: sus puntos se calculan una vez. Antes cada
    /// consulta releia y recalculaba todas las versiones.
    /// </summary>
    [Fact]
    public async Task HistoryReadsEachVersionOnlyOnce()
    {
        var provider = new CountingProvider(new DummySnapshotProvider(DummyPortfolio.DefaultSeed, 20));
        using var service = new PortfolioService(provider, Rules);

        await service.GetPortfolioHistoryAsync(PortfolioScope.Full);
        var readsAfterFirst = provider.Reads;

        for (var i = 0; i < 5; i++)
        {
            await service.GetPortfolioHistoryAsync(PortfolioScope.Full);
            await service.GetHistoryAsync("14381", PortfolioScope.Full);
        }

        Assert.Equal(readsAfterFirst, provider.Reads);
    }

    [Fact]
    public async Task EveryReadHonoursTheScope()
    {
        using var service = new PortfolioService(new DummySnapshotProvider(DummyPortfolio.DefaultSeed, 60), Rules);

        var full = await service.GetSnapshotAsync(PortfolioScope.Full);
        var lob = full.Applications.First(a => a.Lob is not null).Lob!;
        var scope = PortfolioScope.FromHeader("lob:" + Uri.EscapeDataString(lob));
        var outside = full.Applications.First(a => !string.Equals(a.Lob, lob, StringComparison.OrdinalIgnoreCase));

        var scoped = await service.GetSnapshotAsync(scope);
        Assert.NotEmpty(scoped.Applications);
        Assert.All(scoped.Applications, a => Assert.Equal(lob, a.Lob, ignoreCase: true));

        Assert.Null(await service.FindAsync(outside.EaiCode, scope));
        Assert.Null(await service.GetScoreAsync(outside.EaiCode, scope));
        Assert.DoesNotContain(await service.GetAllScoresAsync(scope), s => s.EaiCode == outside.EaiCode);
        Assert.Empty((await service.GetSnapshotAsync(PortfolioScope.None)).Applications);
    }

    /// <summary>Retiene la primera lectura hasta que la prueba publica otra version.</summary>
    private sealed class GatedProvider : IApplicationSnapshotProvider
    {
        public const int PublishedCount = 30;

        private readonly DummySnapshotProvider _before = new(DummyPortfolio.DefaultSeed, 20);
        private readonly DummySnapshotProvider _after = new(DummyPortfolio.DefaultSeed, PublishedCount);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;
        private volatile bool _published;

        public TaskCompletionSource FirstReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Provider => "Gated";

        public void Publish() => _published = true;

        public void ReleaseFirstRead() => _release.TrySetResult();

        public Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default)
            => (_published ? _after : _before).GetVersionsAsync(cancellationToken);

        public async Task<SnapshotSet> GetAllAsync(string? versionId, CancellationToken cancellationToken = default)
        {
            // Lo que habia al empezar a leer, aunque se publique durante la lectura.
            var source = _published ? _after : _before;

            if (Interlocked.Increment(ref _reads) == 1)
            {
                FirstReadStarted.TrySetResult();
                await _release.Task;
            }

            return await source.GetAllAsync(versionId, cancellationToken);
        }
    }

    private sealed class CountingProvider(IApplicationSnapshotProvider inner) : IApplicationSnapshotProvider
    {
        private int _reads;

        public int Reads => _reads;

        public string Provider => inner.Provider;

        public Task<IReadOnlyList<SnapshotVersion>> GetVersionsAsync(CancellationToken cancellationToken = default)
            => inner.GetVersionsAsync(cancellationToken);

        public Task<SnapshotSet> GetAllAsync(string? versionId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _reads);
            return inner.GetAllAsync(versionId, cancellationToken);
        }
    }
}
