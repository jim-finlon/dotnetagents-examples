namespace SalesArena.Orchestrator.Ledger;

/// <summary>
/// Append-only event log. The single source of truth for the leaderboard
/// (SA-02-04) and the replay engine (SA-04-01). Subscribers attach via the
/// orchestrator's pub/sub seam; the ledger itself only ingests + queries.
/// </summary>
public interface IArenaLedger : IAsyncDisposable
{
    /// <summary>Append one event. Returns the event with its <see cref="ArenaEvent.Id"/> populated.</summary>
    Task<ArenaEvent> AppendAsync(ArenaEvent evt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Append many events as a single transaction. Returns the events with
    /// assigned ids in input order. All-or-nothing: if any insert fails, no
    /// rows are persisted.
    /// </summary>
    Task<IReadOnlyList<ArenaEvent>> AppendManyAsync(
        IEnumerable<ArenaEvent> events,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Query events matching <paramref name="filter"/>. Results are streamed
    /// chronologically (oldest-first by default) so consumers can scan large
    /// contests without buffering the whole result set.
    /// </summary>
    IAsyncEnumerable<ArenaEvent> QueryAsync(
        ArenaEventFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Count events matching <paramref name="filter"/>.</summary>
    Task<long> CountAsync(
        ArenaEventFilter filter,
        CancellationToken cancellationToken = default);
}
