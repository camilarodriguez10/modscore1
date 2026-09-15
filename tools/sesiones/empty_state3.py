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

edit(r"src\channel\Modernization.ChannelApi\Program.cs", [
    ("""builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();""",
     """builder.Services.AddProblemDetails();

// El unico error del sistema con una accion asociada: se propaga como 409 para
// que la pantalla ofrezca la carga en lugar de un panel de error.
builder.Services.AddExceptionHandler<Modernization.ChannelApi.NoDataExceptionHandler>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();"""),
])

print("done")
