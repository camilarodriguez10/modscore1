using Microsoft.EntityFrameworkCore;
using Modernization.Domain.Rules;

namespace Modernization.Sources.Database;

/// <summary>
/// Versiones de reglas en la base: una fila por version, escrita en una transaccion
/// que comprueba antes que la version base siga siendo la ultima.
/// </summary>
/// <remarks>
/// La clave primaria es el identificador de version, asi que dos publicaciones que
/// calcularon el mismo siguiente numero no pueden entrar las dos: la segunda falla y
/// se reporta como conflicto, igual que en el almacen de archivos.
/// </remarks>
public sealed class DatabaseRulesStore(IDbContextFactory<ModernizationDbContext> contexts) : IRulesStore
{
    public string Kind => "Database";

    public async Task<StoredRulesVersion?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var latest = LatestOf(await IdsAsync(db, cancellationToken).ConfigureAwait(false));

        return latest is null ? null : await FindAsync(db, latest, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredRulesVersion?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!RulesVersionId.IsWellFormed(id))
        {
            return null;
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await FindAsync(db, id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StoredRulesVersion>> ListAsync(int max, CancellationToken cancellationToken = default)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // El orden es por numero de version, que no es el orden de texto de la columna:
        // v1.10 va despues de v1.9. Son pocas filas; se ordena en memoria.
        var ids = (await IdsAsync(db, cancellationToken).ConfigureAwait(false))
            .OrderByDescending(id => id, Comparer<string>.Create(RulesVersionId.Compare))
            .Take(Math.Max(0, max))
            .ToList();

        var rows = await db.RulesVersions
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows
            .OrderByDescending(r => r.Id, Comparer<string>.Create(RulesVersionId.Compare))
            .Select(ToStored)];
    }

    public async Task PublishAsync(
        StoredRulesVersion version,
        string? expectedLatestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(version);

        if (!RulesVersionId.IsWellFormed(version.Id))
        {
            throw new ArgumentException("El identificador de version no tiene la forma vMayor.Menor.", nameof(version));
        }

        await using var db = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var ids = await IdsAsync(db, cancellationToken).ConfigureAwait(false);

            if (!string.Equals(LatestOf(ids), expectedLatestId, StringComparison.Ordinal)
                || ids.Contains(version.Id, StringComparer.Ordinal))
            {
                throw new RulesStoreConflictException();
            }

            db.RulesVersions.Add(new RulesVersionRow
            {
                Id = version.Id,
                BasedOn = version.BasedOn,
                PublishedAtUtc = version.PublishedAt.UtcDateTime,
                PublishedBy = version.PublishedBy,
                Reason = version.Reason,
                Ruleset = version.Ruleset,
                Levers = version.Levers,
                Roadmap = version.Roadmap,
                Changes = version.Changes,
                Impact = version.Impact,
                Analytics = version.Analytics,
            });

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            // Clave duplicada o base bloqueada por la otra escritura: perdio la carrera.
            throw new RulesStoreConflictException("Otra publicacion gano la carrera.", ex);
        }
    }

    // -----------------------------------------------------------------------

    private static Task<List<string>> IdsAsync(ModernizationDbContext db, CancellationToken cancellationToken)
        => db.RulesVersions.AsNoTracking().Select(v => v.Id).ToListAsync(cancellationToken);

    private static string? LatestOf(List<string> ids)
        => ids.Count == 0 ? null : ids.MaxBy(id => id, Comparer<string>.Create(RulesVersionId.Compare));

    private static async Task<StoredRulesVersion?> FindAsync(
        ModernizationDbContext db,
        string id,
        CancellationToken cancellationToken)
    {
        var row = await db.RulesVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? null : ToStored(row);
    }

    private static StoredRulesVersion ToStored(RulesVersionRow row) => new()
    {
        Id = row.Id,
        BasedOn = row.BasedOn,
        PublishedAt = new DateTimeOffset(DateTime.SpecifyKind(row.PublishedAtUtc, DateTimeKind.Utc)),
        PublishedBy = row.PublishedBy,
        Reason = row.Reason,
        Ruleset = row.Ruleset,
        Levers = row.Levers,
        Roadmap = row.Roadmap,
        Changes = row.Changes,
        Impact = row.Impact,
        Analytics = row.Analytics,
    };
}

/// <summary>Una version publicada de las reglas.</summary>
public sealed class RulesVersionRow
{
    public required string Id { get; set; }

    public string? BasedOn { get; set; }

    /// <summary>Siempre UTC, por la misma razon que las cargas: SQLite no ordena DateTimeOffset.</summary>
    public required DateTime PublishedAtUtc { get; set; }

    public required string PublishedBy { get; set; }

    public required string Reason { get; set; }

    public required string Ruleset { get; set; }

    public required string Levers { get; set; }

    public required string Roadmap { get; set; }

    public required string Changes { get; set; }

    public required string Impact { get; set; }

    /// <summary>
    /// Definiciones de las seis preguntas. Vacio en las versiones publicadas antes de la
    /// migracion que agrego la columna: esas usan los valores de fabrica.
    /// </summary>
    public string Analytics { get; set; } = string.Empty;
}
