namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Canonical theatre-cue discriminators. The cue engine maps each
/// <see cref="Ledger.ArenaEvent"/> to one of these, then asks
/// <see cref="IArenaCueScriptResolver"/> for the line the narrator speaks.
/// </summary>
public static class CueKinds
{
    public const string ContestOpened = "ContestOpened";
    public const string DealClosed = "DealClosed";
    public const string GlengarryDripped = "GlengarryDripped";
    public const string PersonaPromoted = "PersonaPromoted";
    public const string PersonaDropped = "PersonaDropped";
    public const string BellRung = "BellRung";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ContestOpened, DealClosed, GlengarryDripped, PersonaPromoted, PersonaDropped, BellRung,
    };
}
