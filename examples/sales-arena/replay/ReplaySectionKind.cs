namespace SalesArena.Replay;

/// <summary>
/// Canonical replay sections. Each maps to an <c>ISectionBuilder</c> + a
/// forkable header template at <c>samples/sales-arena/replay/templates/{kind}.md</c>.
/// </summary>
public enum ReplaySectionKind
{
    /// <summary>Final leaderboard table at end-of-contest.</summary>
    Leaderboard,

    /// <summary>One block per persona with their deal log.</summary>
    PersonaDealLog,

    /// <summary>The biggest deal that almost slipped (largest Lost-deal value).</summary>
    ClosestCall,

    /// <summary>Persona that climbed the most positions over the contest's leaderboard snapshots.</summary>
    BestComeback,

    /// <summary>Single touch that flipped the most value (TouchSent → next won-deal pairing).</summary>
    MvpTouch,

    /// <summary>Celebration of the runner-up; the closest moment they came to taking Cadillac.</summary>
    SteakKnivesShowcase,

    /// <summary>LLM-generated comedy roast — higher-ranked persona critiques lower-ranked in voice.</summary>
    Roast,
}
