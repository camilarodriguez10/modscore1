import io, os
root = r"C:\Users\kmiro\Downloads\mod score"

def edit(rel, pairs):
    p = os.path.join(root, rel)
    s = io.open(p, encoding='utf-8').read()
    for old, new in pairs:
        assert old in s, f"NO MATCH in {rel}: {old[:90]!r}"
        s = s.replace(old, new, 1)
    io.open(p, 'w', encoding='utf-8', newline='').write(s)
    print("ok", rel)

edit(r"tests\Modernization.Sources.ContractTests\FileSnapshotProviderTests.cs", [
    ("""    [Fact]
    public async Task EmptyRootIsReportedAsSuch()
    {
        var provider = new FileSnapshotProvider(Path.Combine(_root, "vacio"));

        Assert.Empty(await provider.GetVersionsAsync());
        await Assert.ThrowsAsync<SourceSchemaException>(() => provider.GetAllAsync());
    }""",
     """    /// <summary>
    /// Sin ninguna carga, el adaptador no falla: declara que no hay datos. La
    /// distincion es la que permite que la pantalla de cargas siga funcionando y
    /// el usuario pueda salir del estado inicial.
    /// </summary>
    [Fact]
    public async Task EmptyRootIsReportedAsSuch()
    {
        var provider = new FileSnapshotProvider(Path.Combine(_root, "vacio"));

        Assert.Empty(await provider.GetVersionsAsync());
        await Assert.ThrowsAsync<NoSnapshotAvailableException>(() => provider.GetAllAsync());
    }"""),
])

print("done")
