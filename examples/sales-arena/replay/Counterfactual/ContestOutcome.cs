namespace SalesArena.Replay.Counterfactual;

/// <summary>
/// Per-persona outcome of a simulated contest. The counterfactual diff is
/// computed by subtracting these values between the original + mutated runs.
/// </summary>
public sealed record PersonaOutcome(
    string Persona,
    int TouchesSent,
    int MeetingsHeld,
    int DealsWon,
    int DealsLost,
    decimal RevenueUsd,
    int FinalPosition);

public sealed record ContestOutcome(
    string ContestId,
    IReadOnlyList<PersonaOutcome> Personas)
{
    public PersonaOutcome? Find(string persona) =>
        Personas.SingleOrDefault(p => p.Persona == persona);
}

/// <summary>Per-persona delta between two contest outcomes.</summary>
public sealed record PersonaOutcomeDiff(
    string Persona,
    int TouchesDelta,
    int MeetingsDelta,
    int DealsWonDelta,
    int DealsLostDelta,
    decimal RevenueDeltaUsd,
    int PositionDelta);

public sealed record ContestOutcomeDiff(
    string OriginalContestId,
    string CounterfactualContestId,
    IReadOnlyList<PersonaOutcomeDiff> Personas)
{
    public bool IsZero => Personas.All(p =>
        p.TouchesDelta == 0 &&
        p.MeetingsDelta == 0 &&
        p.DealsWonDelta == 0 &&
        p.DealsLostDelta == 0 &&
        p.RevenueDeltaUsd == 0m &&
        p.PositionDelta == 0);
}

public sealed record CounterfactualResult(
    string OriginalContestId,
    CounterfactualMutation Mutation,
    ContestOutcome Original,
    ContestOutcome Counterfactual,
    ContestOutcomeDiff Diff);
