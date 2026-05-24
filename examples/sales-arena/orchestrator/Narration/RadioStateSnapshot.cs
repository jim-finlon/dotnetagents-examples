namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// State the operator feeds the radio every tick. The radio doesn't poll the
/// ledger directly — it stays cheap and pure. The orchestrator (or a thin
/// scheduled job) computes the snapshot and asks the radio whether to speak.
/// </summary>
public sealed record RadioStateSnapshot
{
    public required string ContestId { get; init; }
    public required TimeSpan TimeSinceLastBell { get; init; }
    public required TimeSpan ContestElapsed { get; init; }

    /// <summary>
    /// Persona → consecutive touches since the last inbound reply. Personas
    /// with 3+ qualify for a PersonaMomentum cue.
    /// </summary>
    public IReadOnlyDictionary<string, int> PersonaTouchStreaks { get; init; }
        = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Leads with no inbound activity for the configured aging window. The
    /// radio picks the oldest one for a LeadAged cue.
    /// </summary>
    public IReadOnlyList<AgedLead> AgedLeads { get; init; } = Array.Empty<AgedLead>();

    /// <summary>
    /// Persona currently in the lead — used by ContestProgress copy.
    /// </summary>
    public string? FrontRunner { get; init; }

    /// <summary>
    /// Optional persona running second + the close-gap (for "X is still
    /// in research mode" style filler).
    /// </summary>
    public string? Runner_up { get; init; }

    public int FrontRunnerCloses { get; init; }
}

public sealed record AgedLead(string LeadId, TimeSpan SilentFor);
