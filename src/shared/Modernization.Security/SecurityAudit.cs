using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Modernization.Security;

/// <summary>
/// Eventos de seguridad con identificador estable, para que el SIEM alerte por
/// EventId sin depender del texto (ASVS V7).
/// </summary>
/// <remarks>
/// Se registra el subject seudonimo del usuario, nunca su nombre ni su correo.
/// Cada metodo limpia sus valores antes de registrarlos: un salto de linea dentro
/// de una ruta no puede fabricar una entrada de log falsa, y ningun llamador puede
/// olvidarse de hacerlo.
/// </remarks>
public static partial class SecurityAudit
{
    /// <summary>Categoria de log propia: permite enrutarla a un destino inmutable.</summary>
    public const string Category = "Modernization.Audit";

    private const int MaxValueLength = 200;

    public static ILogger CreateLogger(ILoggerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return factory.CreateLogger(Category);
    }

    public static void SessionStarted(ILogger logger, string? user)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var cleanUser = Clean(user);
            LogSessionStarted(logger, cleanUser);
        }
    }

    public static void SessionEnded(ILogger logger, string? user)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var cleanUser = Clean(user);
            LogSessionEnded(logger, cleanUser);
        }
    }

    public static void SessionExpired(ILogger logger, string? user, DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var cleanUser = Clean(user);
            LogSessionExpired(logger, cleanUser, startedAt);
        }
    }

    public static void AccessDenied(ILogger logger, string? user, string? path)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanUser = Clean(user);
            var cleanPath = Clean(path);
            LogAccessDenied(logger, cleanUser, cleanPath);
        }
    }

    public static void CsrfRejected(ILogger logger, string? user, string? path)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanUser = Clean(user);
            var cleanPath = Clean(path);
            LogCsrfRejected(logger, cleanUser, cleanPath);
        }
    }

    public static void RateLimited(ILogger logger, string? partition, string? path)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanPartition = Clean(partition);
            var cleanPath = Clean(path);
            LogRateLimited(logger, cleanPartition, cleanPath);
        }
    }

    public static void UploadPublished(ILogger logger, string? user, string? versionId, int fileCount, int applicationCount)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var cleanUser = Clean(user);
            var cleanVersionId = Clean(versionId);
            LogUploadPublished(logger, cleanUser, cleanVersionId, fileCount, applicationCount);
        }
    }

    public static void UploadRejected(ILogger logger, string? user, int errorCount)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanUser = Clean(user);
            LogUploadRejected(logger, cleanUser, errorCount);
        }
    }

    public static void ScopeDenied(ILogger logger, string? user, string? path)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanUser = Clean(user);
            var cleanPath = Clean(path);
            LogScopeDenied(logger, cleanUser, cleanPath);
        }
    }

    /// <summary>
    /// Una version de reglas publicada: quien, cual, sobre cual, cuantos cambios y el
    /// impacto simulado. El detalle de los cambios queda guardado con la version.
    /// </summary>
    public static void RulesPublished(
        ILogger logger,
        string? user,
        string? versionId,
        string? basedOn,
        int changeCount,
        int up,
        int down,
        decimal? averageDelta)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var cleanUser = Clean(user);
            var cleanVersion = Clean(versionId);
            var cleanBase = Clean(basedOn);
            LogRulesPublished(logger, cleanUser, cleanVersion, cleanBase, changeCount, up, down, averageDelta);
        }
    }

    public static void RulesRejected(ILogger logger, string? user, string? reason)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanUser = Clean(user);
            var cleanReason = Clean(reason);
            LogRulesRejected(logger, cleanUser, cleanReason);
        }
    }

    public static void StepUpCompleted(ILogger logger, string? user)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Information))
        {
            var cleanUser = Clean(user);
            LogStepUpCompleted(logger, cleanUser);
        }
    }

    public static void StepUpRequired(ILogger logger, string? user, string? path)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (logger.IsEnabled(LogLevel.Warning))
        {
            var cleanUser = Clean(user);
            var cleanPath = Clean(path);
            LogStepUpRequired(logger, cleanUser, cleanPath);
        }
    }

    /// <summary>
    /// Neutraliza un valor externo antes de registrarlo: reemplaza caracteres de
    /// control y de formato, y lo acota en longitud.
    /// </summary>
    public static string Clean(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var length = Math.Min(value.Length, MaxValueLength);
        var builder = new StringBuilder(length + 1);

        for (var i = 0; i < length; i++)
        {
            var character = value[i];
            builder.Append(
                char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format
                    ? '?'
                    : character);
        }

        if (value.Length > MaxValueLength)
        {
            builder.Append('…');
        }

        return builder.ToString();
    }

    [LoggerMessage(EventId = 4001, Level = LogLevel.Information,
        Message = "Sesion iniciada. usuario={User}")]
    private static partial void LogSessionStarted(ILogger logger, string user);

    [LoggerMessage(EventId = 4002, Level = LogLevel.Information,
        Message = "Sesion cerrada. usuario={User}")]
    private static partial void LogSessionEnded(ILogger logger, string user);

    [LoggerMessage(EventId = 4003, Level = LogLevel.Information,
        Message = "Sesion expirada por tiempo absoluto. usuario={User} iniciada={StartedAt}")]
    private static partial void LogSessionExpired(ILogger logger, string user, DateTimeOffset startedAt);

    [LoggerMessage(EventId = 4101, Level = LogLevel.Warning,
        Message = "Acceso denegado. usuario={User} ruta={Path}")]
    private static partial void LogAccessDenied(ILogger logger, string user, string path);

    [LoggerMessage(EventId = 4102, Level = LogLevel.Warning,
        Message = "Token CSRF ausente o invalido. usuario={User} ruta={Path}")]
    private static partial void LogCsrfRejected(ILogger logger, string user, string path);

    [LoggerMessage(EventId = 4103, Level = LogLevel.Warning,
        Message = "Limite de peticiones alcanzado. particion={Partition} ruta={Path}")]
    private static partial void LogRateLimited(ILogger logger, string partition, string path);

    [LoggerMessage(EventId = 4201, Level = LogLevel.Information,
        Message = "Carga publicada. usuario={User} version={VersionId} archivos={FileCount} aplicaciones={ApplicationCount}")]
    private static partial void LogUploadPublished(ILogger logger, string user, string versionId, int fileCount, int applicationCount);

    [LoggerMessage(EventId = 4202, Level = LogLevel.Warning,
        Message = "Carga rechazada. usuario={User} errores={ErrorCount}")]
    private static partial void LogUploadRejected(ILogger logger, string user, int errorCount);

    [LoggerMessage(EventId = 4203, Level = LogLevel.Warning,
        Message = "Alcance insuficiente en la capa System. usuario={User} ruta={Path}")]
    private static partial void LogScopeDenied(ILogger logger, string user, string path);

    [LoggerMessage(EventId = 4004, Level = LogLevel.Information,
        Message = "Reautenticacion completada. usuario={User}")]
    private static partial void LogStepUpCompleted(ILogger logger, string user);

    [LoggerMessage(EventId = 4104, Level = LogLevel.Warning,
        Message = "Operacion sensible sin reautenticacion reciente. usuario={User} ruta={Path}")]
    private static partial void LogStepUpRequired(ILogger logger, string user, string path);

    [LoggerMessage(EventId = 4211, Level = LogLevel.Information,
        Message = "Reglas publicadas. usuario={User} version={VersionId} base={BasedOn} cambios={ChangeCount} suben={Up} bajan={Down} deltaPromedio={AverageDelta}")]
    private static partial void LogRulesPublished(
        ILogger logger,
        string user,
        string versionId,
        string basedOn,
        int changeCount,
        int up,
        int down,
        decimal? averageDelta);

    [LoggerMessage(EventId = 4212, Level = LogLevel.Warning,
        Message = "Publicacion de reglas rechazada. usuario={User} motivo={Reason}")]
    private static partial void LogRulesRejected(ILogger logger, string user, string reason);
}
