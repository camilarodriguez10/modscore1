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

# ------------------------------------------------- adaptador de archivos
edit(r"src\system\Modernization.Sources\Files\FileSnapshotProvider.cs", [
    ("""        if (versions.Count == 0)
        {
            throw new SourceSchemaException(
                $"No hay ninguna carga en '{_rootDirectory}'. Sube los archivos antes de calcular.");
        }""",
     """        if (versions.Count == 0)
        {
            // El estado inicial legitimo del adaptador, no un fallo: la pantalla
            // de cargas tiene que seguir funcionando para poder salir de el.
            throw new NoSnapshotAvailableException(
                "Todavia no hay ninguna carga publicada. Sube los archivos de las fuentes "
                + "para calcular el portafolio.");
        }"""),
])

print("done")
