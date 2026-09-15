using Microsoft.AspNetCore.Http;

namespace Modernization.Security;

/// <summary>
/// Respuestas de error con un <c>type</c> estable.
/// </summary>
/// <remarks>
/// El <c>type</c> es el contrato: <c>urn:modscore:problem:invalid-filter</c> no
/// cambia aunque cambie la redaccion. El <c>title</c> y el <c>detail</c> van en
/// ingles desde la Fase 11 porque los leen tanto la interfaz como quien integra
/// contra el API (ADR 0017). Ninguno hace eco de la entrada del usuario.
/// </remarks>
public static class ApiProblem
{
    public const string TypePrefix = "urn:modscore:problem:";

    public static IResult Create(int statusCode, string code, string title, string detail)
        => Results.Problem(detail: detail, statusCode: statusCode, title: title, type: TypePrefix + code);

    /// <summary>El <c>type</c> completo de un codigo.</summary>
    public static string TypeOf(string code) => TypePrefix + code;
}
