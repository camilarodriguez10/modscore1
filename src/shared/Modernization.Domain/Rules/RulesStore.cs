using System.Globalization;

namespace Modernization.Domain.Rules;

/// <summary>
/// Una version publicada de las reglas, tal como se guarda.
/// </summary>
/// <remarks>
/// Los documentos viajan como JSON y el almacen no los interpreta: igual que las
/// cargas, una version publicada es inmutable, y quien la lee la valida al
/// construir el modelo. Asi el almacen de archivos y el de base de datos guardan
/// exactamente lo mismo y no pueden discrepar sobre que significa una regla.
/// </remarks>
public sealed record StoredRulesVersion
{
    /// <summary>Identificador con la forma <c>v1.4</c>. Crece con cada publicacion.</summary>
    public required string Id { get; init; }

    /// <summary>La version vigente cuando se preparo esta. Null para la primera.</summary>
    public string? BasedOn { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>Subject seudonimo de quien publico.</summary>
    public required string PublishedBy { get; init; }

    /// <summary>Motivo del cambio, escrito por quien publico.</summary>
    public required string Reason { get; init; }

    /// <summary>Ruleset de scoring.</summary>
    public required string Ruleset { get; init; }

    /// <summary>Ajustes de las palancas del what-if.</summary>
    public required string Levers { get; init; }

    /// <summary>Catalogo MAPS con sus parametros.</summary>
    public required string Roadmap { get; init; }

    /// <summary>Que cambio respecto a la version anterior, como avisos.</summary>
    public required string Changes { get; init; }

    /// <summary>El impacto simulado al publicar.</summary>
    public required string Impact { get; init; }
}

/// <summary>
/// Almacen de versiones de reglas. Mismo patron que las fuentes: un puerto, y un
/// adaptador por persistencia (memoria, archivos, base de datos).
/// </summary>
/// <remarks>
/// Solo se agrega. Publicar nunca edita una version existente, y volver atras es
/// publicar otra vez lo que habia (plan secc. 8bis.3).
/// </remarks>
public interface IRulesStore
{
    /// <summary>Nombre del adaptador, para diagnostico.</summary>
    string Kind { get; }

    /// <summary>La version mas reciente, o null si nadie publico todavia.</summary>
    Task<StoredRulesVersion?> GetLatestAsync(CancellationToken cancellationToken = default);

    Task<StoredRulesVersion?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Las mas recientes primero, hasta <paramref name="max"/>.</summary>
    Task<IReadOnlyList<StoredRulesVersion>> ListAsync(int max, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publica una version nueva si la ultima sigue siendo <paramref name="expectedLatestId"/>.
    /// </summary>
    /// <exception cref="RulesStoreConflictException">
    /// Alguien publico entre medio, o el identificador ya existe.
    /// </exception>
    Task PublishAsync(StoredRulesVersion version, string? expectedLatestId, CancellationToken cancellationToken = default);
}

/// <summary>Otra publicacion gano la carrera: la version base ya no es la vigente.</summary>
public sealed class RulesStoreConflictException : Exception
{
    public RulesStoreConflictException() : base("La version base ya no es la vigente.") { }

    public RulesStoreConflictException(string message) : base(message) { }

    public RulesStoreConflictException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Identificadores de version de reglas: <c>v</c>, mayor, punto, menor.
/// </summary>
/// <remarks>
/// En el adaptador de archivos el identificador es un directorio, asi que su forma
/// se valida por lista blanca antes de usarlo en cualquier ruta. Y crece de forma
/// monotona: dos publicaciones simultaneas calculan el mismo siguiente numero, y
/// solo una puede escribirlo.
/// </remarks>
public static class RulesVersionId
{
    public const int MaxLength = 16;

    private const int MaxMajorDigits = 4;
    private const int MaxMinorDigits = 6;

    public static bool IsWellFormed(string? id) => TryParse(id, out _, out _);

    /// <summary>El siguiente identificador: <c>v1.3</c> da <c>v1.4</c>.</summary>
    public static string Next(string current)
    {
        if (!TryParse(current, out var major, out var minor))
        {
            throw new ArgumentException("El identificador de version no tiene la forma vMayor.Menor.", nameof(current));
        }

        return string.Create(CultureInfo.InvariantCulture, $"v{major}.{minor + 1}");
    }

    /// <summary>Compara por numero, no por texto: <c>v1.10</c> es posterior a <c>v1.9</c>.</summary>
    public static int Compare(string? left, string? right)
    {
        var leftOk = TryParse(left, out var leftMajor, out var leftMinor);
        var rightOk = TryParse(right, out var rightMajor, out var rightMinor);

        if (!leftOk || !rightOk)
        {
            return leftOk.CompareTo(rightOk);
        }

        return leftMajor != rightMajor ? leftMajor.CompareTo(rightMajor) : leftMinor.CompareTo(rightMinor);
    }

    private static bool TryParse(string? id, out int major, out int minor)
    {
        major = 0;
        minor = 0;

        if (id is not { Length: > 3 and <= MaxLength } || id[0] != 'v')
        {
            return false;
        }

        var dot = id.IndexOf('.', StringComparison.Ordinal);

        if (dot < 2 || dot == id.Length - 1)
        {
            return false;
        }

        var majorText = id.AsSpan(1, dot - 1);
        var minorText = id.AsSpan(dot + 1);

        return majorText.Length <= MaxMajorDigits
            && minorText.Length <= MaxMinorDigits
            && IsDigits(majorText)
            && IsDigits(minorText)
            && int.TryParse(majorText, NumberStyles.None, CultureInfo.InvariantCulture, out major)
            && int.TryParse(minorText, NumberStyles.None, CultureInfo.InvariantCulture, out minor);
    }

    private static bool IsDigits(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Almacen en memoria. Lo usa el adaptador Dummy y las pruebas: se pierde al
/// reiniciar, igual que los datos generados que acompana.
/// </summary>
public sealed class InMemoryRulesStore : IRulesStore
{
    private readonly Lock _gate = new();
    private readonly List<StoredRulesVersion> _versions = [];

    public string Kind => "Memory";

    public Task<StoredRulesVersion?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(Latest());
        }
    }

    public Task<StoredRulesVersion?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_versions.Find(v => string.Equals(v.Id, id, StringComparison.Ordinal)));
        }
    }

    public Task<IReadOnlyList<StoredRulesVersion>> ListAsync(int max, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            IReadOnlyList<StoredRulesVersion> newest = [.. _versions
                .OrderByDescending(v => v.Id, Comparer<string>.Create(RulesVersionId.Compare))
                .Take(Math.Max(0, max))];

            return Task.FromResult(newest);
        }
    }

    public Task PublishAsync(StoredRulesVersion version, string? expectedLatestId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (!RulesVersionId.IsWellFormed(version.Id))
        {
            throw new ArgumentException("El identificador de version no tiene la forma vMayor.Menor.", nameof(version));
        }

        lock (_gate)
        {
            if (!string.Equals(Latest()?.Id, expectedLatestId, StringComparison.Ordinal)
                || _versions.Exists(v => string.Equals(v.Id, version.Id, StringComparison.Ordinal)))
            {
                throw new RulesStoreConflictException();
            }

            _versions.Add(version);
        }

        return Task.CompletedTask;
    }

    private StoredRulesVersion? Latest()
        => _versions.Count == 0
            ? null
            : _versions.MaxBy(v => v.Id, Comparer<string>.Create(RulesVersionId.Compare));
}
