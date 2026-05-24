using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Replay.Sections;

/// <summary>
/// "🔪 Steak Knives Showcase." A 1-paragraph celebration of the #2 persona
/// — invisible by default between the Cadillac winner and the YouAreFired
/// tier. Cites the moment they were closest to taking Cadillac (smallest
/// revenue gap from a LeaderboardSnapshot in the contest window).
/// </summary>
public sealed class SteakKnivesShowcaseSectionBuilder : ISectionBuilder
{
    public ReplaySectionKind Kind => ReplaySectionKind.SteakKnivesShowcase;

    public Task<SectionResult> BuildAsync(SectionContext ctx, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(TemplateLoader.LoadHeader(Kind, ctx));
        sb.AppendLine();

        if (ctx.FinalLeaderboard.Entries.Count < 2)
        {
            sb.AppendLine("*Need at least 2 personas to crown a runner-up.*");
            var emptyResult = new ReplaySection(Kind, "Steak Knives Showcase", sb.ToString().TrimEnd());
            return Task.FromResult(new SectionResult(emptyResult, Array.Empty<ReplayHighlight>()));
        }

        var runnerUp = ctx.FinalLeaderboard.Entries[1];
        var winner = ctx.FinalLeaderboard.Entries[0];

        // Find the moment the runner-up was closest to the winner via leaderboard snapshots.
        var (closestSnapshotAt, smallestGap, closestEvent) = FindClosestMoment(ctx, runnerUp.Persona, winner.Persona);

        var revenueGap = winner.RevenueUsd - runnerUp.RevenueUsd;
        sb.AppendLine($"- **{runnerUp.Persona}** earned the Steak Knives with **${runnerUp.RevenueUsd:N0}** ({runnerUp.DealsWon} wins, {runnerUp.WinRate:P0} win rate).");
        sb.AppendLine($"- Final gap to Cadillac: **${revenueGap:N0}** behind {winner.Persona}.");

        if (closestSnapshotAt is not null && smallestGap is not null)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "- Closest they came: a **${0:N0}** gap at {1:u} — keep that energy.",
                smallestGap.Value, closestSnapshotAt.Value));
        }
        else
        {
            sb.AppendLine("- They held second the whole way — a steady hand on a noisy floor.");
        }

        var highlight = new ReplayHighlight(
            Source: Kind,
            Headline: $"Steak Knives go to {runnerUp.Persona} — ${revenueGap:N0} away from the Cadillac.",
            Persona: runnerUp.Persona,
            LeadId: null,
            ValueUsd: runnerUp.RevenueUsd,
            OccurredAtUtc: closestSnapshotAt ?? ctx.FinalLeaderboard.AsOfUtc);

        var section = new ReplaySection(Kind, "Steak Knives Showcase", sb.ToString().TrimEnd());
        return Task.FromResult(new SectionResult(section, new[] { highlight }));
    }

    private static (DateTimeOffset? At, decimal? Gap, ArenaEvent? SourceEvent) FindClosestMoment(
        SectionContext ctx, string runnerUp, string winner)
    {
        decimal? smallestGap = null;
        DateTimeOffset? closestAt = null;
        ArenaEvent? closestEvent = null;

        foreach (var evt in ctx.AllEvents.Where(e => e.Kind == ArenaEventKinds.LeaderboardSnapshot))
        {
            var payload = evt.GetPayload<LeaderboardSnapshotPayload>();
            if (payload is null) continue;

            var winnerEntry = payload.Entries.FirstOrDefault(r => string.Equals(r.Persona, winner, StringComparison.Ordinal));
            var runnerUpEntry = payload.Entries.FirstOrDefault(r => string.Equals(r.Persona, runnerUp, StringComparison.Ordinal));
            if (winnerEntry is null || runnerUpEntry is null) continue;

            // Only count moments where the runner-up was actually trailing the winner.
            if (winnerEntry.RevenueUsd <= runnerUpEntry.RevenueUsd) continue;

            var gap = winnerEntry.RevenueUsd - runnerUpEntry.RevenueUsd;
            if (smallestGap is null || gap < smallestGap.Value)
            {
                smallestGap = gap;
                closestAt = evt.OccurredAtUtc;
                closestEvent = evt;
            }
        }

        return (closestAt, smallestGap, closestEvent);
    }
}
