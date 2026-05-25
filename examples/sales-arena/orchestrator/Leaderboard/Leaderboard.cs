namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Read-only snapshot of the leaderboard at <see cref="AsOfUtc"/> under
/// <see cref="ScoringConfigId"/>. Entries are sorted by position ascending
/// (Cadillac is index 0).
/// </summary>
public sealed record Leaderboard(
    string ContestId,
    string ScoringConfigId,
    DateTimeOffset AsOfUtc,
    IReadOnlyList<LeaderboardRow> Entries);

/// <summary>
/// One persona's row in the Leaderboard. Carries the score, position, tier
/// classification, and a compact view of the underlying stats so consumers
/// can render rich cards without an extra round-trip.
/// </summary>
public sealed record LeaderboardRow(
    int Position,
    LeaderboardTier Tier,
    string Persona,
    double Score,
    decimal RevenueUsd,
    int DealsWon,
    int DealsLost,
    double WinRate);
