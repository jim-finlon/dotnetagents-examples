namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Fired by <see cref="LeaderboardEngine"/> when a recompute produces a
/// different tier-assignment than the previous compute under the same
/// scoring config. Subscribers include the Narrator (SA-02-06) — when a
/// persona promotes into Cadillac, the bell rings.
/// </summary>
public sealed class LeaderboardChangedEventArgs : EventArgs
{
    public LeaderboardChangedEventArgs(
        Leaderboard previous,
        Leaderboard current,
        IReadOnlyList<PersonaTierChange> changes)
    {
        Previous = previous;
        Current = current;
        Changes = changes;
    }

    public Leaderboard Previous { get; }
    public Leaderboard Current { get; }
    public IReadOnlyList<PersonaTierChange> Changes { get; }
}

/// <summary>One persona's tier movement between two computes.</summary>
public sealed record PersonaTierChange(
    string Persona,
    int FromPosition,
    int ToPosition,
    LeaderboardTier FromTier,
    LeaderboardTier ToTier);
