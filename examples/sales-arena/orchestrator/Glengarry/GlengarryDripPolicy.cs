namespace SalesArena.Orchestrator.Glengarry;

/// <summary>
/// Configuration for the Glengarry premium-leads drip mechanic. Drives the
/// Arena's "leaderboard matters" dynamic: the top-tier persona is rewarded
/// with fresh premium leads; the bottom-tier persona loses leads back to the
/// pool. Without this, the leaderboard would be cosmetic.
/// </summary>
/// <param name="DripWindow">
/// How often the drip cycle runs. Default 24h. The runner refuses to drip
/// twice within the same window per persona — cooldown prevents oscillation.
/// </param>
/// <param name="DripCount">
/// How many premium leads to drip to the top-tier persona per cycle.
/// Default 5.
/// </param>
/// <param name="BottomRevokeCount">
/// How many leads to revoke from the bottom-tier persona per cycle. Default
/// 3. Capped by the bottom persona's current holdings (no-op if they own
/// fewer than this).
/// </param>
/// <param name="PremiumTierName">
/// Tier-name filter passed to <see cref="LeadPool.ILeadPool.AssignAsync"/>
/// when claiming premium leads. Default "glengarry".
/// </param>
/// <param name="HonorBottomCooldown">
/// When true (default), the bottom-tier persona must hold their tier for
/// one full window before leads are revoked. Prevents thrashing for a
/// persona that just barely dropped to the bottom.
/// </param>
public sealed record GlengarryDripPolicy(
    TimeSpan DripWindow,
    int DripCount = 5,
    int BottomRevokeCount = 3,
    string PremiumTierName = "glengarry",
    bool HonorBottomCooldown = true)
{
    /// <summary>Sensible defaults for the 7-day flagship contest.</summary>
    public static GlengarryDripPolicy Default { get; } = new(
        DripWindow: TimeSpan.FromHours(24),
        DripCount: 5,
        BottomRevokeCount: 3);

    /// <summary>Demo-mode (compressed time): drip every 5 simulated minutes.</summary>
    public static GlengarryDripPolicy Demo { get; } = new(
        DripWindow: TimeSpan.FromMinutes(5),
        DripCount: 3,
        BottomRevokeCount: 2);
}
