namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Ambient (between-the-bells) cue discriminators. Distinct from
/// <see cref="CueKinds"/> so the radio's stream of filler doesn't compete
/// with the event-driven cues for the same script files.
/// </summary>
public static class AmbientCueKinds
{
    public const string ContestProgress = "ContestProgress";
    public const string PersonaMomentum = "PersonaMomentum";
    public const string LeadAged = "LeadAged";
    public const string GenericFiller = "GenericFiller";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ContestProgress, PersonaMomentum, LeadAged, GenericFiller,
    };
}
