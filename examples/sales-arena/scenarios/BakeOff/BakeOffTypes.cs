using SalesArena.Replay.Counterfactual;

namespace SalesArena.BakeOff;

/// <summary>
/// One persona's side-by-side outcome. Carries per-product outcomes from
/// the underlying simulator plus the local winner verdict for that persona.
/// </summary>
public sealed record PersonaBakeOffResult(
    string Persona,
    PersonaOutcome WithProductA,
    PersonaOutcome WithProductB,
    string? PreferredProduct,
    decimal RevenueDeltaUsd);

public sealed record BakeOffAggregate(
    int PersonasPreferringA,
    int PersonasPreferringB,
    int PersonasTied,
    decimal TotalRevenueDeltaUsd,
    string? OverallVerdict);

public sealed record BakeOffResult(
    string BakeOffId,
    ProductProfile ProductA,
    ProductProfile ProductB,
    int Seed,
    int LeadPoolSize,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<PersonaBakeOffResult> PerPersona,
    BakeOffAggregate Aggregate)
{
    public bool ContainsConfidentialData =>
        ProductA.ContainsConfidentialData || ProductB.ContainsConfidentialData;
}
