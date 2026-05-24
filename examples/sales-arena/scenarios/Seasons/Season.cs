namespace SalesArena.Seasons;

/// <summary>
/// Configuration knobs a season applies on top of the base contest scoring.
/// Weights multiply per-event contribution; persona buffs are flat additive
/// rating bonuses applied at season-leaderboard render time (they don't
/// permanently inflate ELO).
/// </summary>
public sealed record SeasonScoringWeights(
    double RevenueWeight,
    double DealsWeight,
    double ConversionWeight,
    double BurnoutPenaltyPerHourOverThreshold,
    TimeSpan BurnoutPenaltyThreshold)
{
    public static SeasonScoringWeights Default => new(
        RevenueWeight: 1.0,
        DealsWeight: 1.0,
        ConversionWeight: 1.0,
        BurnoutPenaltyPerHourOverThreshold: 0.0,
        BurnoutPenaltyThreshold: TimeSpan.FromHours(99));
}

/// <summary>
/// Optional persona-keyed flat rating adjustment applied when rendering a
/// season leaderboard. Themed seasons use this to favor specific persona
/// archetypes (e.g., Wolf-of-Wall-Street buffs the high-volume hunter).
/// </summary>
public sealed record PersonaBuff(string Persona, double FlatRatingBonus);

public sealed record Season(
    string Id,
    string Name,
    SeasonScoringWeights Weights,
    IReadOnlyList<PersonaBuff> PersonaBuffs,
    TimeSpan Duration,
    DateTimeOffset StartedAtUtc,
    string Description);
