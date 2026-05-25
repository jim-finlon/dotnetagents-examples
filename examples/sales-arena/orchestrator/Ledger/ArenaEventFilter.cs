namespace SalesArena.Orchestrator.Ledger;

/// <summary>
/// Optional filter for <see cref="IArenaLedger.QueryAsync"/>. All fields are
/// AND-combined; null means "match anything." Use the static factory methods
/// for the common shapes.
/// </summary>
public sealed record ArenaEventFilter(
    string? ContestId = null,
    string? Kind = null,
    string? LeadId = null,
    string? Persona = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int? Limit = null,
    bool DescendingTime = false)
{
    /// <summary>Match all events in a single contest.</summary>
    public static ArenaEventFilter ForContest(string contestId) => new(ContestId: contestId);

    /// <summary>Match all events for one deal (drill-down).</summary>
    public static ArenaEventFilter ForLead(string contestId, string leadId) =>
        new(ContestId: contestId, LeadId: leadId);

    /// <summary>Match all events for one persona in a contest.</summary>
    public static ArenaEventFilter ForPersona(string contestId, string persona) =>
        new(ContestId: contestId, Persona: persona);

    /// <summary>Match all events of a single kind in a contest.</summary>
    public static ArenaEventFilter OfKind(string contestId, string kind) =>
        new(ContestId: contestId, Kind: kind);
}
