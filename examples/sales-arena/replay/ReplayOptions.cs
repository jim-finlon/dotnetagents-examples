using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Replay;

/// <summary>Options for <see cref="IReplayGenerator.GenerateAsync"/>.</summary>
public sealed record ReplayOptions(
    string ContestId,
    IScoringConfig FinalScoring,
    DateTimeOffset? AsOfUtc = null,
    IReadOnlyList<ReplaySectionKind>? IncludeSections = null,
    string? TemplateDir = null,
    string? ContestDisplayName = null)
{
    /// <summary>The 7 canonical sections in standard order.</summary>
    public static IReadOnlyList<ReplaySectionKind> DefaultSections { get; } = new[]
    {
        ReplaySectionKind.Leaderboard,
        ReplaySectionKind.PersonaDealLog,
        ReplaySectionKind.SteakKnivesShowcase,
        ReplaySectionKind.ClosestCall,
        ReplaySectionKind.BestComeback,
        ReplaySectionKind.MvpTouch,
        ReplaySectionKind.Roast,
    };

    /// <summary>Sections to render. Defaults to <see cref="DefaultSections"/> when null.</summary>
    public IReadOnlyList<ReplaySectionKind> ResolvedSections =>
        IncludeSections ?? DefaultSections;
}
