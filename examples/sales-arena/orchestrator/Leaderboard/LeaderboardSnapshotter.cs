using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Persists <see cref="Leaderboard"/> snapshots into the ledger so the replay
/// engine (SA-04-01) can reconstruct "what did the board look like at 11:42?"
/// without re-aggregating events.
///
/// <para>Call-driven, not timer-driven — the orchestrator's tick loop
/// (SA-02-01) calls <see cref="SnapshotAsync"/> on its own cadence
/// (default 60s; demo mode every tick). Keeping the timer out of this class
/// makes it trivially testable.</para>
/// </summary>
public sealed class LeaderboardSnapshotter
{
    private readonly IArenaLedger _ledger;

    public LeaderboardSnapshotter(IArenaLedger ledger)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    /// <summary>
    /// Serialize the leaderboard into a <see cref="ArenaEventKinds.LeaderboardSnapshot"/>
    /// event and append to the ledger. Returns the appended event (with assigned id).
    /// </summary>
    public Task<ArenaEvent> SnapshotAsync(
        Leaderboard board,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(board);

        var payload = new LeaderboardSnapshotPayload(
            Entries: board.Entries
                .Select(r => new LeaderboardEntry(
                    Persona: r.Persona,
                    Position: r.Position,
                    Tier: LeaderboardTierNames.ToName(r.Tier),
                    RevenueUsd: r.RevenueUsd,
                    DealsWon: r.DealsWon,
                    DealsLost: r.DealsLost,
                    ConversionRate: r.WinRate))
                .ToList(),
            ScoringConfigId: board.ScoringConfigId);

        var evt = new ArenaEvent
        {
            ContestId = board.ContestId,
            Kind = ArenaEventKinds.LeaderboardSnapshot,
            OccurredAtUtc = board.AsOfUtc,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        };

        return _ledger.AppendAsync(evt, cancellationToken);
    }
}
