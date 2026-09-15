using Microsoft.AspNetCore.Http;

namespace Modernization.Security;

/// <summary>
/// Permisos del usuario final que la capa Channel ya resolvio y reenvia a la System.
/// </summary>
/// <remarks>
/// Es defensa en profundidad, igual que el alcance: la capa Channel decide con los
/// claims de la sesion, y la System vuelve a exigir que el permiso llegue declarado
/// por un llamador con token de servicio. Solo se aceptan valores de una lista
/// blanca; cualquier otro se ignora y nunca amplia nada.
/// </remarks>
public static class ForwardedGrants
{
    public const string HeaderName = "X-Portfolio-Grants";

    /// <summary>Puede administrar las reglas de scoring.</summary>
    public const string RulesAdmin = "rules.admin";

    /// <summary>Volvio a autenticarse hace poco: puede ejecutar operaciones sensibles.</summary>
    public const string Reauthenticated = "reauthenticated";

    private const int MaxHeaderLength = 256;

    private static readonly string[] Known = [RulesAdmin, Reauthenticated];

    public static string ToHeaderValue(IEnumerable<string> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);

        return string.Join(' ', grants.Where(g => Known.Contains(g, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal));
    }

    /// <summary>True si un llamador de servicio declaro el permiso para esta peticion.</summary>
    public static bool Has(HttpContext context, string grant)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ServiceCaller.Is(context.User) || !Known.Contains(grant, StringComparer.Ordinal))
        {
            return false;
        }

        var header = context.Request.Headers[HeaderName].ToString();

        return header.Length <= MaxHeaderLength
            && header.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(grant, StringComparer.Ordinal);
    }
}
