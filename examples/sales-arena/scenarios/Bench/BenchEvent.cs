namespace SalesArena.Bench;

/// <summary>
/// New ledger-event-kind discriminators introduced by SA-08-17. The
/// orchestrator will pick these up as <c>ArenaEventKinds.PersonaPromoted</c>
/// and <c>ArenaEventKinds.PersonaRelegated</c> when SA-02 absorbs them;
/// keeping the strings here avoids a cross-package coupling until then.
/// </summary>
public static class BenchEventKinds
{
    public const string PersonaPromoted = "PersonaPromoted";
    public const string PersonaRelegated = "PersonaRelegated";
    public const string BenchEvicted = "BenchEvicted";
}

public sealed record BenchEvent(
    string Kind,
    string Persona,
    string? RelatedPersona,
    string Reason,
    DateTimeOffset OccurredAtUtc);
