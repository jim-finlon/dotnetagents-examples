using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Orchestrator.Glengarry;

/// <summary>
/// Runs the Glengarry-drip cycle: top-tier persona gets fresh premium leads,
/// bottom-tier persona loses leads back to the pool. Ledger events
/// (<c>GlengarryLeadDripped</c>, <c>LeadsRevoked</c>) record every mutation.
/// </summary>
public interface IGlengarryDripPolicyRunner
{
    /// <summary>
    /// Run a single drip cycle against <paramref name="leaderboard"/>. Honors
    /// the policy's cooldown — if the previous cycle for this top-persona ran
    /// less than <c>DripWindow</c> ago, this call no-ops with reason
    /// <see cref="GlengarryDripSkipReasons.NotDueYet"/>.
    /// </summary>
    Task<GlengarryDripDecision> RunDripCycleAsync(
        Leaderboard.Leaderboard leaderboard,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);
}
