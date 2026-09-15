using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Distributed;

namespace Modernization.ChannelApi.Auth;

/// <summary>
/// Sesiones guardadas del lado servidor. La cookie solo lleva una clave aleatoria.
/// </summary>
/// <remarks>
/// Con la cookie como unico almacen, cerrar sesion solo la borra del navegador: una
/// copia robada sigue valiendo hasta que expira. Con el ticket en el servidor,
/// cerrar sesion lo elimina y la copia deja de servir (ASVS 3.3.1, ADR 0011).
/// <para>
/// El ticket se cifra con Data Protection antes de guardarse: lleva la identidad y
/// los permisos del usuario, y un cache distribuido (Redis en despliegue) no es
/// lugar para tenerlos en claro.
/// </para>
/// </remarks>
public sealed class DistributedTicketStore(
    IDistributedCache cache,
    IDataProtectionProvider protection,
    SessionLifetime lifetime,
    TimeProvider clock) : ITicketStore
{
    private const string KeyPrefix = "modscore:session:";

    private readonly IDataProtector _protector =
        protection.CreateProtector("Modernization.ChannelApi.SessionTicket.v1");

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        // 256 bits aleatorios: la clave es la sesion, no puede ser adivinable.
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await RenewAsync(key, ticket).ConfigureAwait(false);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var payload = _protector.Protect(TicketSerializer.Default.Serialize(ticket));

        // El vencimiento absoluto del cache sigue al inicio de sesion, no a la
        // ultima renovacion: una sesion activa no puede extenderse para siempre.
        var startedAt = SessionLifetime.StartedAt(ticket.Properties) ?? clock.GetUtcNow();
        var absolute = startedAt + lifetime.Absolute;
        var options = new DistributedCacheEntryOptions { SlidingExpiration = lifetime.Idle };

        // El cache compara con su propio reloj y no admite un vencimiento pasado. Si
        // la sesion ya lo supero, le queda un minuto como mucho: la validacion del
        // principal la rechaza antes de que ese minuto importe.
        if (absolute > DateTimeOffset.UtcNow)
        {
            options.AbsoluteExpiration = absolute;
        }
        else
        {
            options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
        }

        return cache.SetAsync(KeyPrefix + key, payload, options);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var payload = await cache.GetAsync(KeyPrefix + key).ConfigureAwait(false);

        if (payload is null)
        {
            return null;
        }

        try
        {
            return TicketSerializer.Default.Deserialize(_protector.Unprotect(payload));
        }
        catch (CryptographicException)
        {
            // Claves rotadas o dato alterado: la sesion deja de existir, sin mas.
            return null;
        }
    }

    public Task RemoveAsync(string key) => cache.RemoveAsync(KeyPrefix + key);
}

/// <summary>
/// Tiempos de vida de la sesion. ASVS 3.3.2 (nivel 2): reautenticacion tras 30
/// minutos de inactividad y, aun con uso continuo, tras 12 horas.
/// </summary>
public sealed record SessionLifetime(TimeSpan Idle, TimeSpan Absolute)
{
    /// <summary>Marca del inicio de sesion. Sobrevive a las renovaciones del ticket.</summary>
    public const string StartedAtKey = ".modscore.started";

    public static SessionLifetime Default { get; } = new(TimeSpan.FromMinutes(30), TimeSpan.FromHours(12));

    public static DateTimeOffset? StartedAt(AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        return properties.Items.TryGetValue(StartedAtKey, out var raw)
            && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
                ? value
                : null;
    }

    public static void MarkStarted(AuthenticationProperties properties, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(properties);

        if (!properties.Items.ContainsKey(StartedAtKey))
        {
            properties.Items[StartedAtKey] = now.ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
