from patchlib import patch

patch(r"src\system\Modernization.SystemApi\Program.cs", [
("""using System.Text.Json.Serialization;
using Modernization.Domain.Rules;""",
"""using System.Text.Json.Serialization;
using Modernization.Domain.Code;
using Modernization.Domain.Rules;"""),
("""using Modernization.Domain.Sources;
using Modernization.Security;""",
"""using Modernization.Domain.Sources;
using Modernization.Insights;
using Modernization.Security;"""),
("""builder.Services.AddSingleton(TimeProvider.System);
""",
"""builder.Services.AddSingleton(TimeProvider.System);

// ------------------------------------------------------------------ codigo
//
// El inventario de repositorios de la pantalla Code (ADR 0026). Hasta que exista el
// recolector de GitHub Enterprise hay dos opciones: datos de demostracion o nada. Los de
// demostracion solo existen en Development: fuera de ahi, un dato inventado se leeria
// como real, y el proceso no arranca.

var codeProvider = builder.Configuration["CodeInventory:Provider"]
    ?? (builder.Environment.IsDevelopment() ? SampleCodeInventoryProvider.Name : NoCodeInventoryProvider.Name);

switch (codeProvider)
{
    case SampleCodeInventoryProvider.Name when builder.Environment.IsDevelopment():
        builder.Services.AddSingleton<ICodeInventoryProvider, SampleCodeInventoryProvider>();
        break;

    case SampleCodeInventoryProvider.Name:
        throw new InvalidOperationException(
            $"'CodeInventory:Provider' = '{SampleCodeInventoryProvider.Name}' solo se admite en Development "
            + $"(entorno '{builder.Environment.EnvironmentName}'): son repositorios de demostracion.");

    case NoCodeInventoryProvider.Name:
        builder.Services.AddSingleton<ICodeInventoryProvider, NoCodeInventoryProvider>();
        break;

    default:
        throw new InvalidOperationException(
            $"'CodeInventory:Provider' tiene el valor '{codeProvider}'. Valores validos: "
            + $"{SampleCodeInventoryProvider.Name} (solo Development), {NoCodeInventoryProvider.Name}.");
}

// Las fechas de fin de vida: la foto de endoflife.date que viaja con el despliegue.
builder.Services.AddSingleton(_ => EndOfLifeCatalog.Default);
"""),
("""app.MapDashboardEndpoints();
""",
"""app.MapDashboardEndpoints();
app.MapCodeEndpoints();
"""),
])
