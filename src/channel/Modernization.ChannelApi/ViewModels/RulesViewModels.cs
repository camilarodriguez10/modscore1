using Modernization.Rules;
using Modernization.SystemApi.Contracts;

namespace Modernization.ChannelApi.ViewModels;

/// <summary>
/// Pantalla 09 — administracion de reglas.
/// </summary>
/// <remarks>
/// A diferencia del resto de las pantallas, esta no reordena nada: el documento de
/// reglas ya tiene la forma en que se edita, y se entrega tal cual lo produce la capa
/// System. Lo que agrega esta capa es lo que solo ella sabe: si la sesion puede
/// publicar ahora o necesita reautenticarse primero.
/// </remarks>
public sealed record RulesAdminView(
    RulesVersionResponse Active,
    IReadOnlyList<RulesVersionResponse> Versions,
    RulesDocument Document,
    string Store,
    bool Reauthenticated,
    int ReauthenticationMinutes,
    RulesLimitsView Limits);

/// <summary>Los topes que valida el servidor, para que la pantalla los muestre antes de chocar con ellos.</summary>
public sealed record RulesLimitsView(
    decimal TotalWeight,
    int MaxBands,
    int MaxBandLabelLength,
    int MinReasonLength,
    int MaxReasonLength,
    int MaxWeeks,
    int MaxLeverParameter)
{
    public static RulesLimitsView Current { get; } = new(
        RulesLimits.TotalWeight,
        RulesLimits.MaxBands,
        RulesLimits.MaxBandLabelLength,
        RulesLimits.MinReasonLength,
        RulesLimits.MaxReasonLength,
        RulesLimits.MaxWeeks,
        Modernization.WhatIf.LeverSettings.MaxParameter);
}
