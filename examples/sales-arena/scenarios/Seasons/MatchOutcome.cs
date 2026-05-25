namespace SalesArena.Seasons;

/// <summary>The result a head-to-head ELO update is computed from.</summary>
public enum MatchOutcome
{
    /// <summary>Player A won the head-to-head.</summary>
    AWins = 1,
    /// <summary>Player B won the head-to-head.</summary>
    BWins = 2,
    /// <summary>Tie / no winner — both ratings drift toward the expectation.</summary>
    Draw = 3,
}

/// <summary>
/// One ELO update event. Carries the persona pair, outcome, optional
/// season scope, and an opaque match id so audit/replay can reconstruct
/// the rating timeline.
/// </summary>
public sealed record MatchRecord(
    string PersonaA,
    string PersonaB,
    MatchOutcome Outcome,
    string MatchId,
    string? SeasonId,
    DateTimeOffset PlayedAtUtc);
