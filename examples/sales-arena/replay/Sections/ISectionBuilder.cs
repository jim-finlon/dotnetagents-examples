using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Replay.Sections;

/// <summary>
/// Builds one <see cref="ReplaySection"/> from the contest's events + final
/// leaderboard. Section builders are stateless — the engine wires them once
/// and calls them with the per-contest context object.
/// </summary>
public interface ISectionBuilder
{
    ReplaySectionKind Kind { get; }

    Task<SectionResult> BuildAsync(SectionContext ctx, CancellationToken cancellationToken = default);
}

/// <summary>Context passed to every section builder.</summary>
public sealed record SectionContext(
    string ContestId,
    string ContestDisplayName,
    DateTimeOffset GeneratedAtUtc,
    Leaderboard FinalLeaderboard,
    IReadOnlyList<ArenaEvent> AllEvents,
    string? TemplateDir);

/// <summary>One section builder's output.</summary>
public sealed record SectionResult(
    ReplaySection Section,
    IReadOnlyList<ReplayHighlight> Highlights);
