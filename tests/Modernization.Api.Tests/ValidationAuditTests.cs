using System.Net;
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
