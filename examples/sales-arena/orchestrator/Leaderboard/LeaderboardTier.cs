namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Tier classification on the Cadillac board. Tier names are also persisted in
/// the <see cref="Ledger.LeaderboardSnapshotPayload"/> + rendered by the
/// Manager UI (SA-03-03) with color-coding.
/// </summary>
public enum LeaderboardTier
{
    /// <summary>Position 1 — the Cadillac winner of the contest window.</summary>
    Cadillac,

    /// <summary>Positions 2..ceil(n/2) — the consolation tier.</summary>
    SteakKnives,

    /// <summary>Bottom half — these reps are losing leads to the drip mechanic.</summary>
    YouAreFired,
}

/// <summary>String-name helpers for the persisted ledger payloads.</summary>
public static class LeaderboardTierNames
{
    public const string Cadillac = "Cadillac";
    public const string SteakKnives = "SteakKnives";
    public const string YouAreFired = "YouAreFired";

    public static string ToName(LeaderboardTier tier) => tier switch
    {
        LeaderboardTier.Cadillac => Cadillac,
        LeaderboardTier.SteakKnives => SteakKnives,
        LeaderboardTier.YouAreFired => YouAreFired,
        _ => throw new ArgumentOutOfRangeException(nameof(tier)),
    };
}
