"""Fase 15 - ventana de cambio: la capa System acepta secreto y JWT a la vez."""
from pathlib import Path

root = Path(r"C:\Users\kmiro\Downloads\mod score")


def patch(rel, pairs):
    path = root / rel
    text = path.read_text(encoding="utf-8", newline="")
    crlf = "\r\n" in text
    for old, new in pairs:
        if crlf:
            old, new = old.replace("\n", "\r\n"), new.replace("\n", "\r\n")
        if old not in text:
            raise SystemExit("ancla no encontrada en " + rel + ":\n" + old[:160])
        text = text.replace(old, new, 1)
    path.write_text(text, encoding="utf-8", newline="")
    print("parcheado", rel)


patch("src/shared/Modernization.Security/ServiceTokenValidation.cs", [
    (
        """    /// <summary>Token de vida corta emitido por APIM o el IdP (ADR 0020, opcion C).</summary>
    Jwt,
}""",
        """    /// <summary>Token de vida corta emitido por APIM o el IdP (ADR 0020, opcion C).</summary>
    Jwt,

    /// <summary>
    /// Los dos a la vez, solo durante el cambio: la capa Channel puede estar en una
    /// version y la System en otra, y el cambio no puede pedir un corte.
    /// </summary>
    Both,
}""",
    ),
])

# El validador compuesto vive junto a los otros dos.
(root / "src/shared/Modernization.Security/EitherServiceTokenValidator.cs").write_text(
    '''namespace Modernization.Security;

/// <summary>
/// Acepta el secreto compartido o el token de vida corta, en ese orden.
/// </summary>
/// <remarks>
/// Es la ventana del cambio a APIM y nada mas: mientras dure, una replica vieja de la
/// capa Channel sigue funcionando y una nueva ya usa el token de APIM. Cuando todas
/// hayan cambiado, se pasa a <see cref="ServiceTokenMode.Jwt"/> y el secreto se retira
/// del gestor de secretos, que es lo que cierra H-16.
/// <para>
/// El orden importa poco para la seguridad —los dos validan de verdad— y mucho para el
/// costo: el secreto se compara en memoria y el JWT puede pedir llaves.
/// </para>
/// </remarks>
public sealed class EitherServiceTokenValidator(
    StaticServiceTokenValidator shared,
    JwtServiceTokenValidator issued) : IServiceTokenValidator
{
    public async ValueTask<ServiceTokenValidation> ValidateAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var first = await shared.ValidateAsync(token, cancellationToken).ConfigureAwait(false);

        return first.IsValid
            ? first
            : await issued.ValidateAsync(token, cancellationToken).ConfigureAwait(false);
    }
}
'''.replace("\n", "\r\n"),
    encoding="utf-8",
    newline="",
)
print("escrito EitherServiceTokenValidator.cs")

patch("src/shared/Modernization.Security/ServiceTokenAuthentication.cs", [
    (
        """        if (Mode == ServiceTokenMode.Jwt)
        {""",
        """        if (Mode == ServiceTokenMode.Jwt)
        {
            // Con APIM el secreto compartido no existe: lo que hay que validar son
            // el emisor, la audiencia y las llaves, y de eso se ocupa su validador.
            return;
        }

        if (false)
        {""",
    ),
])

# Limpia el bloque duplicado que deja el parche anterior.
path = root / "src/shared/Modernization.Security/ServiceTokenAuthentication.cs"
text = path.read_text(encoding="utf-8", newline="")
duplicate = """        if (false)
        {
            // Con APIM el secreto compartido no existe: lo que hay que validar son
            // el emisor, la audiencia y las llaves, y de eso se ocupa su validador.
            return;
        }

"""
duplicate = duplicate.replace("\n", "\r\n") if "\r\n" in text else duplicate
if duplicate not in text:
    raise SystemExit("no se encontro el bloque a limpiar")
path.write_text(text.replace(duplicate, "", 1), encoding="utf-8", newline="")
print("ServiceTokenOptions: validacion por modo")

patch("src/system/Modernization.SystemApi/Program.cs", [
    (
        """var jwtServiceToken = string.Equals(
    builder.Configuration["ServiceToken:Mode"],
    "Jwt",
    StringComparison.OrdinalIgnoreCase);""",
        """// Static (hoy), Jwt (con APIM) o Both, que es la ventana del cambio (ADR 0022).
var serviceTokenMode = Enum.TryParse<ServiceTokenMode>(
    builder.Configuration["ServiceToken:Mode"],
    ignoreCase: true,
    out var configuredMode)
        ? configuredMode
        : ServiceTokenMode.Static;

var jwtServiceToken = serviceTokenMode is ServiceTokenMode.Jwt or ServiceTokenMode.Both;""",
    ),
    (
        "            options.Mode = jwtServiceToken ? ServiceTokenMode.Jwt : ServiceTokenMode.Static;",
        "            options.Mode = serviceTokenMode;",
    ),
    (
        """    builder.Services.AddSingleton(jwt);
    builder.Services.AddSingleton<IServiceTokenValidator, JwtServiceTokenValidator>();
}
else
{
    builder.Services.AddSingleton<IServiceTokenValidator, StaticServiceTokenValidator>();
}""",
        """    builder.Services.AddSingleton(jwt);
    builder.Services.AddSingleton<JwtServiceTokenValidator>();

    if (serviceTokenMode == ServiceTokenMode.Both)
    {
        builder.Services.AddSingleton<StaticServiceTokenValidator>();
        builder.Services.AddSingleton<IServiceTokenValidator, EitherServiceTokenValidator>();
    }
    else
    {
        builder.Services.AddSingleton<IServiceTokenValidator>(
            sp => sp.GetRequiredService<JwtServiceTokenValidator>());
    }
}
else
{
    builder.Services.AddSingleton<IServiceTokenValidator, StaticServiceTokenValidator>();
}""",
    ),
])
