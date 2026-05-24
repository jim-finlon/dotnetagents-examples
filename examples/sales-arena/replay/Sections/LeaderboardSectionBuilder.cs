using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Replay.Sections;

/// <summary>Renders the final leaderboard table.</summary>
public sealed class LeaderboardSectionBuilder : ISectionBuilder
{
    public ReplaySectionKind Kind => ReplaySectionKind.Leaderboard;

    public Task<SectionResult> BuildAsync(SectionContext ctx, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(TemplateLoader.LoadHeader(Kind, ctx));
        sb.AppendLine();
        sb.AppendLine("| # | Persona | Tier | Revenue | Wins | Losses | Win Rate | Score |");
        sb.AppendLine("|---|---|---|---:|---:|---:|---:|---:|");

        var highlights = new List<ReplayHighlight>();
        foreach (var row in ctx.FinalLeaderboard.Entries)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "| {0} | {1} | {2} | ${3:N0} | {4} | {5} | {6:P0} | {7:N2} |",
                row.Position, row.Persona, TierGlyph(row.Tier), row.RevenueUsd,
                row.DealsWon, row.DealsLost, row.WinRate, row.Score));
        }

        if (ctx.FinalLeaderboard.Entries.Count > 0)
        {
            var top = ctx.FinalLeaderboard.Entries[0];
            highlights.Add(new ReplayHighlight(
                Source: Kind,
                Headline: $"{top.Persona} took the Cadillac with ${top.RevenueUsd:N0} and {top.DealsWon} closed.",
                Persona: top.Persona,
                LeadId: null,
                ValueUsd: top.RevenueUsd,
                OccurredAtUtc: ctx.FinalLeaderboard.AsOfUtc));
        }

        var section = new ReplaySection(Kind, "Final Leaderboard", sb.ToString().TrimEnd());
        return Task.FromResult(new SectionResult(section, highlights));
    }

    private static string TierGlyph(LeaderboardTier tier) => tier switch
    {
        LeaderboardTier.Cadillac     => "🚗 Cadillac",
        LeaderboardTier.SteakKnives  => "🔪 Steak Knives",
        LeaderboardTier.YouAreFired  => "📦 You're Fired",
        _ => tier.ToString(),
    };
}
