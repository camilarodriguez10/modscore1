using Modernization.Sources.Files;

namespace Modernization.Sources.ContractTests;

/// <summary>
/// Una publicacion interrumpida deja su directorio de staging. La siguiente lo limpia,
/// sin tocar el staging de una publicacion en curso ni ninguna version.
/// </summary>
public sealed class FileRulesStoreStagingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "modscore-rules-staging-" + Guid.NewGuid().ToString("N"));

    public FileRulesStoreStagingTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void CleanupRemovesOnlyStaleStaging()
    {
        var now = DateTimeOffset.UtcNow;
        var stale = Directory.CreateDirectory(Path.Combine(_root, ".staging-interrupted"));
        var inFlight = Directory.CreateDirectory(Path.Combine(_root, ".staging-in-flight"));
        var version = Directory.CreateDirectory(Path.Combine(_root, "v1.4"));

        Directory.SetLastWriteTimeUtc(stale.FullName, now.UtcDateTime - TimeSpan.FromHours(2));
        Directory.SetLastWriteTimeUtc(version.FullName, now.UtcDateTime - TimeSpan.FromDays(30));

        using var store = new FileRulesStore(_root);

        Assert.Equal(1, store.CleanupStaleStaging(now));
        Assert.False(Directory.Exists(stale.FullName));
        Assert.True(Directory.Exists(inFlight.FullName));
        Assert.True(Directory.Exists(version.FullName));
    }

    [Fact]
    public void CleanupOnAMissingDirectoryDoesNothing()
    {
        using var store = new FileRulesStore(Path.Combine(_root, "does-not-exist"));

        Assert.Equal(0, store.CleanupStaleStaging(DateTimeOffset.UtcNow));
    }
}
