using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.Sections;

/// <summary>
/// "From the bottom to the board." Reads LeaderboardSnapshot events in time
/// order; picks the persona with the largest position-decrease (climb)
/// from their lowest snapshot to their position in the final leaderboard.
/// </summary>
public sealed class BestComebackSectionBuilder : ISectionBuilder
{
    public ReplaySectionKind Kind => ReplaySectionKind.BestComeback;

    public Task<SectionResult> BuildAsync(SectionContext ctx, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(TemplateLoader.LoadHeader(Kind, ctx));
        sb.AppendLine();

        var snapshots = ctx.AllEvents
            .Where(e => e.Kind == ArenaEventKinds.LeaderboardSnapshot)
            .Select(e => (Event: e, Payload: e.GetPayload<LeaderboardSnapshotPayload>()))
            .Where(t => t.Payload is not null)
            .OrderBy(t => t.Event.OccurredAtUtc)
            .ToList();

        if (snapshots.Count == 0 || ctx.FinalLeaderboard.Entries.Count == 0)
        {
            sb.AppendLine("*Not enough leaderboard snapshots to compute a comeback.*");
            var emptyResult = new ReplaySection(Kind, "Best Comeback", sb.ToString().TrimEnd());
            return Task.FromResult(new SectionResult(emptyResult, Array.Empty<ReplayHighlight>()));
        }

        // For each persona, find their worst (highest-numbered) position in any snapshot
        // BEFORE the final leaderboard.
        var worstByPersona = new Dictionary<string, (int Position, DateTimeOffset At)>(StringComparer.Ordinal);
        foreach (var (evt, payload) in snapshots)
        {
            foreach (var entry in payload!.Entries)
            {
                if (worstByPersona.TryGetValue(entry.Persona, out var current))
                {
                    if (entry.Position > current.Position)
                        worstByPersona[entry.Persona] = (entry.Position, evt.OccurredAtUtc);
                }
                else
                {
                    worstByPersona[entry.Persona] = (entry.Position, evt.OccurredAtUtc);
                }
            }
        }

        // Compute the climb: (worst position - final position). Bigger is better.
        var climbs = ctx.FinalLeaderboard.Entries
            .Select(final =>
            {
                if (!worstByPersona.TryGetValue(final.Persona, out var worst)) return (final.Persona, Climb: 0, FromPosition: final.Position, ToPosition: final.Position, At: ctx.FinalLeaderboard.AsOfUtc);
                return (final.Persona, Climb: worst.Position - final.Position, FromPosition: worst.Position, ToPosition: final.Position, At: worst.At);
            })
            .OrderByDescending(t => t.Climb)
            .ThenBy(t => t.Persona, StringComparer.Ordinal)
            .ToList();

        var champion = climbs[0];
        if (champion.Climb <= 0)
        {
            sb.AppendLine("*No persona climbed from a worse position. The leaders led from start to finish.*");
            var noClimbResult = new ReplaySection(Kind, "Best Comeback", sb.ToString().TrimEnd());
            return Task.FromResult(new SectionResult(noClimbResult, Array.Empty<ReplayHighlight>()));
        }

        sb.AppendLine($"- **{champion.Persona}** climbed from position **#{champion.FromPosition}** (worst snapshot at {champion.At:u}) to **#{champion.ToPosition}** at end-of-contest.");
        sb.AppendLine($"- Net climb: **{champion.Climb}** position(s).");

        var highlight = new ReplayHighlight(
            Source: Kind,
            Headline: $"{champion.Persona} climbed {champion.Climb} positions to finish #{champion.ToPosition}.",
            Persona: champion.Persona,
            LeadId: null,
            ValueUsd: null,
            OccurredAtUtc: champion.At);

        var section = new ReplaySection(Kind, "Best Comeback", sb.ToString().TrimEnd());
        return Task.FromResult(new SectionResult(section, new[] { highlight }));
    }
}
