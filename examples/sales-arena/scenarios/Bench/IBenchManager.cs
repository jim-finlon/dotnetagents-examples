namespace SalesArena.Bench;

/// <summary>
/// Roster state machine: who's on the active floor, who's on the reserve
/// bench, how they got there. Pure state — the orchestrator drives the
/// per-contest ticks.
/// </summary>
public interface IBenchManager
{
    IReadOnlyList<BenchRosterEntry> Active { get; }
    IReadOnlyList<BenchRosterEntry> Reserve { get; }

    /// <summary>Promote a persona from reserve to active. Throws if the active floor is full.</summary>
    BenchEvent Promote(string persona, string reason, DateTimeOffset now);

    /// <summary>Relegate an active persona to the reserve bench.</summary>
    BenchEvent Relegate(string persona, string reason, DateTimeOffset now);

    /// <summary>
    /// Apply end-of-contest ELO data. Increments the consecutive-below counter
    /// for any active persona whose rating dipped below the configured floor;
    /// resets it for personas at or above. Returns 0+ relegation events
    /// triggered by personas that crossed the consecutive-contest threshold.
    /// </summary>
    IReadOnlyList<BenchEvent> RecordContestEnd(
        IReadOnlyDictionary<string, double> ratingsAtContestEnd,
        DateTimeOffset now);

    /// <summary>Add a new persona to the reserve bench. Returns an eviction event when FIFO eviction trims an older bencher.</summary>
    BenchEvent? AddToReserve(string persona, DateTimeOffset now);

    /// <summary>True if persona is on the active floor.</summary>
    bool IsActive(string persona);
    /// <summary>True if persona is on the reserve bench.</summary>
    bool IsReserve(string persona);
}
