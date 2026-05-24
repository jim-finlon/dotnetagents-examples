namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Pluggable scoring algorithm. The <see cref="LeaderboardEngine"/> aggregates
/// ledger events into <see cref="PersonaStats"/>; the scoring config produces
/// the single ranking number from those stats.
///
/// <para>Higher score = higher rank. Ties broken by revenue, then deals-won,
/// then persona name ascending (deterministic ordering for replay).</para>
/// </summary>
public interface IScoringConfig
{
    /// <summary>Stable id (e.g. "ByRevenue"). Persisted in LeaderboardSnapshotPayload.</summary>
    string Id { get; }

    /// <summary>Human-readable name shown in the Manager UI metric dropdown.</summary>
    string Name { get; }

    /// <summary>Compute the score for one persona.</summary>
    double ComputeScore(PersonaStats stats);
}

/// <summary>Standard scoring configs that ship in the Arena.</summary>
public static class ScoringConfigIds
{
    public const string ByRevenue = "ByRevenue";
    public const string ByDealCount = "ByDealCount";
    public const string ByConversion = "ByConversion";
    public const string ByAeq = "ByAeq";
}
