using Modernization.Domain;
using Modernization.Rules;
using Modernization.SystemApi.Services;

namespace Modernization.SystemApi.Contracts;

/// <summary>Una version de reglas en la lista de versiones.</summary>
public sealed record RulesVersionResponse(
    string Id,
    string? BasedOn,
    DateTimeOffset? PublishedAt,
    string PublishedBy,
    string Reason,
    bool IsActive,
    bool IsBuiltIn,
    IReadOnlyList<Notice> Changes,
    RulesImpactSummary? Impact)
{
    public static RulesVersionResponse From(RulesVersionInfo info, string activeId)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new RulesVersionResponse(
            info.Id,
            info.BasedOn,
            info.PublishedAt,
            info.PublishedBy,
            info.Reason,
            string.Equals(info.Id, activeId, StringComparison.Ordinal),
            info.IsBuiltIn,
            info.Changes,
            info.Impact);
    }
}

/// <summary>Las reglas vigentes, su documento editable y las versiones anteriores.</summary>
public sealed record RulesStateResponse(
    RulesVersionResponse Active,
    IReadOnlyList<RulesVersionResponse> Versions,
    RulesDocument Document,
    string Store);

/// <summary>Una version concreta, para verla o restaurarla.</summary>
public sealed record RulesVersionDocumentResponse(RulesVersionResponse Version, RulesDocument Document);

/// <summary>Un borrador preparado sobre una version, para validarlo y simularlo.</summary>
public sealed record RulesSimulationRequest(string BasedOn, RulesDraft Draft);

/// <summary>Un borrador para publicar, con el motivo del cambio.</summary>
public sealed record RulesPublishRequest(string BasedOn, string Reason, RulesDraft Draft);

public sealed record RulesSimulationResponse(
    bool Valid,
    bool Stale,
    string ActiveVersion,
    IReadOnlyList<Notice> Errors,
    IReadOnlyList<Notice> Changes,
    RulesImpact? Impact)
{
    public static RulesSimulationResponse From(RulesSimulationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new RulesSimulationResponse(
            outcome.Valid,
            outcome.Stale,
            outcome.ActiveVersion,
            outcome.Errors,
            outcome.Changes,
            outcome.Impact);
    }
}

public sealed record RulesPublishResponse(
    bool Published,
    RulesVersionResponse? Version,
    IReadOnlyList<Notice> Errors,
    IReadOnlyList<Notice> Changes,
    RulesImpact? Impact)
{
    public static RulesPublishResponse From(RulesPublishOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return new RulesPublishResponse(
            outcome.Published,
            outcome.Version is null ? null : RulesVersionResponse.From(outcome.Version, outcome.Version.Id),
            outcome.Errors,
            outcome.Changes,
            outcome.Impact);
    }
}
