using System.Text;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay.Sections;

namespace SalesArena.Replay;

/// <summary>
/// Default <see cref="IReplayGenerator"/> implementation. Orchestrates the
/// per-section builders, composes the final Markdown, and exposes the
/// aggregated <see cref="ReplayHighlight"/> list for the narrative rewriter.
/// </summary>
public sealed class ReplayGenerator : IReplayGenerator
{
    private readonly IArenaLedger _ledger;
    private readonly ILeaderboardEngine _leaderboard;
    private readonly IReadOnlyDictionary<ReplaySectionKind, ISectionBuilder> _builders;
    private readonly TimeProvider _time;

    public ReplayGenerator(
        IArenaLedger ledger,
        ILeaderboardEngine leaderboard,
        TimeProvider? time = null,
        IReadOnlyList<ISectionBuilder>? customBuilders = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _leaderboard = leaderboard ?? throw new ArgumentNullException(nameof(leaderboard));
        _time = time ?? TimeProvider.System;

        var defaults = new ISectionBuilder[]
        {
            new LeaderboardSectionBuilder(),
            new PersonaDealLogSectionBuilder(),
            new SteakKnivesShowcaseSectionBuilder(),
            new ClosestCallSectionBuilder(),
            new BestComebackSectionBuilder(),
            new MvpTouchSectionBuilder(),
            new RoastSectionBuilder(),
        };
        var all = customBuilders is null ? defaults : customBuilders.ToArray();
        _builders = all.ToDictionary(b => b.Kind);
    }

    public async Task<ReplayReport> GenerateAsync(ReplayOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ContestId);

        var asOf = options.AsOfUtc ?? _time.GetUtcNow();
        var contestName = string.IsNullOrWhiteSpace(options.ContestDisplayName) ? options.ContestId : options.ContestDisplayName!;

        var finalLeaderboard = await _leaderboard.ComputeAsync(options.ContestId, options.FinalScoring, asOf, cancellationToken).ConfigureAwait(false);

        var allEvents = new List<ArenaEvent>();
        await foreach (var evt in _ledger.QueryAsync(new ArenaEventFilter(ContestId: options.ContestId, ToUtc: asOf), cancellationToken).ConfigureAwait(false))
        {
            allEvents.Add(evt);
        }

        var ctx = new SectionContext(
            ContestId: options.ContestId,
            ContestDisplayName: contestName,
            GeneratedAtUtc: asOf,
            FinalLeaderboard: finalLeaderboard,
            AllEvents: allEvents,
            TemplateDir: options.TemplateDir);

        var sections = new List<ReplaySection>();
        var highlights = new List<ReplayHighlight>();
        foreach (var kind in options.ResolvedSections)
        {
            if (!_builders.TryGetValue(kind, out var builder))
            {
                continue;
            }
            var result = await builder.BuildAsync(ctx, cancellationToken).ConfigureAwait(false);
            sections.Add(result.Section);
            highlights.AddRange(result.Highlights);
        }

        var markdown = AssembleMarkdown(ctx, sections);
        return new ReplayReport(
            ContestId: options.ContestId,
            GeneratedAtUtc: asOf,
            Markdown: markdown,
            Sections: sections,
            Highlights: highlights);
    }

    public async Task<ReplayReport> ExportToFileAsync(ReplayOptions options, string outputPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var report = await GenerateAsync(options, cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllTextAsync(outputPath, report.Markdown, cancellationToken).ConfigureAwait(false);
        return report;
    }

    private static string AssembleMarkdown(SectionContext ctx, IReadOnlyList<ReplaySection> sections)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Sales Arena Replay — {ctx.ContestDisplayName}");
        sb.AppendLine();
        sb.AppendLine($"> *Generated {ctx.GeneratedAtUtc:u}. Contest id: `{ctx.ContestId}`. Always be closing.*");
        sb.AppendLine();
        foreach (var section in sections)
        {
            sb.AppendLine(section.Markdown);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd() + "\n";
    }
}
