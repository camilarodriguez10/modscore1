using Modernization.ChannelApi.Clients;

namespace Modernization.ChannelApi.Endpoints;

/// <summary>
/// Los filtros del portafolio tal como llegan del navegador. Todas las pantallas que
/// acotan —portafolio, roadmap, las seis preguntas, el dashboard y las descargas— los
/// reciben con <c>[AsParameters]</c>: un filtro nuevo se agrega aqui una sola vez.
/// </summary>
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

    /// <summary>Los valores que escribe una persona, para validar su largo de una vez.</summary>
    internal IEnumerable<string?> TextValues =>
        [Lob, Country, Cio, Cre, Region, Status, Capability, Eai, Disposition, Roadmap, Band];

    public ApplicationQuery ToQuery(int page = 1, int pageSize = 50) => new()
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
        Page = page,
        PageSize = pageSize,
    };
}
