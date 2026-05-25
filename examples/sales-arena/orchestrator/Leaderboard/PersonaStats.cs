namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Aggregated metrics for one persona at a point in time. Built by the
/// <see cref="LeaderboardEngine"/> from ledger events; fed to
/// <see cref="IScoringConfig.ComputeScore"/>.
///
/// <para>Pure data — no scoring opinions. Each <see cref="IScoringConfig"/>
/// decides which fields matter and how to weight them.</para>
/// </summary>
public sealed record PersonaStats(
    string Persona,
    decimal RevenueUsd,
    int DealsWon,
    int DealsLost,
    int TouchesSent,
    int LeadsAssigned,
    int LeadsResearched,
    int MeetingsHeld,
    TimeSpan? AverageTimeToClose)
{
    /// <summary>Wins / (wins + losses), 0 when no decisions yet.</summary>
    public double WinRate => (DealsWon + DealsLost) > 0
        ? (double)DealsWon / (DealsWon + DealsLost)
        : 0.0;

    /// <summary>
    /// Average revenue per deal (won + lost). 0 when no deals closed.
    /// Useful for spotting personas that close big-but-rare vs small-but-often.
    /// </summary>
    public decimal AverageDealValue => (DealsWon + DealsLost) > 0
        ? RevenueUsd / (DealsWon + DealsLost)
        : 0m;
}
