namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// A resolved theatre cue ready for the narrator. Carries the script line
/// the narrator will speak plus enough metadata for downstream replay/audit
/// (which persona, which lead, when, what triggered it).
/// </summary>
public sealed record ArenaCue(
    string ContestId,
    string CueKind,
    string Line,
    string? Persona,
    string? LeadId,
    DateTimeOffset TimestampUtc,
    IReadOnlyDictionary<string, string>? Tokens);
