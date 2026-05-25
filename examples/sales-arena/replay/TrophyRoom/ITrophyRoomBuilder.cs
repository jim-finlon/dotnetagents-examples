namespace SalesArena.Replay.TrophyRoom;

/// <summary>
/// Reads the Arena Ledger across all past contests + renders the Trophy Room.
/// Trophy table (date, contest, winner, revenue) + baseball-card section per
/// persona. The Manager UI's "Hall of Fame" route consumes this directly.
/// </summary>
public interface ITrophyRoomBuilder
{
    /// <summary>
    /// Build the Trophy Room from every contest in the ledger.
    /// </summary>
    /// <param name="options">Optional filter (date range, persona subset, max trophies).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TrophyRoomReport> BuildAsync(
        TrophyRoomOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Optional filter for the Trophy Room build.</summary>
public sealed record TrophyRoomOptions(
    DateTimeOffset? SinceUtc = null,
    DateTimeOffset? UntilUtc = null,
    IReadOnlyList<string>? OnlyPersonas = null,
    int? MaxTrophies = null);
