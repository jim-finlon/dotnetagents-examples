namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Reads the ledger, aggregates per-persona stats, ranks them by the
/// supplied scoring config, and emits change events when tiers shift.
///
/// <para>Computation is read-only — the engine never writes to the ledger
/// directly. Snapshot persistence lives in <see cref="LeaderboardSnapshotter"/>
/// so the timer-driven write path is testable in isolation.</para>
/// </summary>
public interface ILeaderboardEngine
{
    /// <summary>
    /// Compute the leaderboard for one contest at <paramref name="asOfUtc"/>
    /// under <paramref name="scoring"/>. Pure read.
    /// </summary>
    Task<Leaderboard> ComputeAsync(
        string contestId,
        IScoringConfig scoring,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fires after a successful <see cref="ComputeAsync"/> that produced a
    /// tier-assignment different from the previous compute under the same
    /// scoring config. Subscribers handle this synchronously; throw to
    /// signal an error.
    /// </summary>
    event EventHandler<LeaderboardChangedEventArgs>? LeaderboardChanged;
}
