namespace Modernization.SystemApi.Services;

/// <summary>
/// Los filtros del portafolio tal como llegan en la URL. Los endpoints que acotan
/// los reciben con <c>[AsParameters]</c>, asi que agregar un filtro es tocar este
/// tipo y <see cref="ApplicationFilter"/>, no cada endpoint.
/// </summary>
/// <remarks>
/// Los nombres de la URL son los de siempre —<c>lob</c>, <c>country</c>, <c>cio</c>,
/// <c>disposition</c>, <c>roadmap</c>, <c>band</c>, <c>criticalOnly</c>— mas los del
/// dashboard ejecutivo: <c>cre</c>, <c>region</c>, <c>status</c>, <c>capability</c> y
/// <c>eai</c>. Un enlace guardado antes sigue significando lo mismo.
/// </remarks>
public sealed class FilterParameters
{
    public string? Lob { get; set; }
    public string? Country { get; set; }
    public string? Cio { get; set; }
    public string? Cre { get; set; }
    public string? Region { get; set; }
    public string? Status { get; set; }
    public string? Capability { get; set; }
    public string? Eai { get; set; }
    public string? Disposition { get; set; }
    public string? Roadmap { get; set; }
    public string? Band { get; set; }
    public bool? CriticalOnly { get; set; }

    public ApplicationFilter ToFilter() => new()
    {
        Lob = Lob,
        Country = Country,
        Cio = Cio,
        Cre = Cre,
        Region = Region,
        Status = Status,
        Capability = Capability,
        EaiCode = Eai,
        Disposition = Disposition,
        Roadmap = Roadmap,
        Band = Band,
        CriticalOnly = CriticalOnly,
    };
}
