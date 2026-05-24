using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.Sections;

/// <summary>Per-persona deal log. One section block per persona with at least one closed deal.</summary>
public sealed class PersonaDealLogSectionBuilder : ISectionBuilder
{
    public ReplaySectionKind Kind => ReplaySectionKind.PersonaDealLog;

    public Task<SectionResult> BuildAsync(SectionContext ctx, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(TemplateLoader.LoadHeader(Kind, ctx));
        sb.AppendLine();

        var dealsByPersona = ctx.AllEvents
            .Where(e => e.Kind == ArenaEventKinds.DealClosed && !string.IsNullOrEmpty(e.Persona))
            .GroupBy(e => e.Persona!, StringComparer.Ordinal)
            .OrderBy(g => ctx.FinalLeaderboard.Entries.FirstOrDefault(r => r.Persona == g.Key)?.Position ?? int.MaxValue);

        var anyDeals = false;
        foreach (var group in dealsByPersona)
        {
            anyDeals = true;
            sb.AppendLine();
            sb.AppendLine($"### {group.Key}");
            sb.AppendLine();
            foreach (var evt in group.OrderBy(e => e.OccurredAtUtc))
            {
                var payload = evt.GetPayload<DealClosedPayload>();
                if (payload is null) continue;

                var outcomeGlyph = string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase) ? "✅" : "❌";
                var valueLabel = payload.ValueUsd is { } v
                    ? string.Format(CultureInfo.InvariantCulture, "${0:N0}", v)
                    : "—";
                var lossReason = payload.LossReason is { Length: > 0 } reason
                    ? $" *({reason})*"
                    : "";
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "- {0} **{1}** — {2} on {3:u} ({4}){5}",
                    outcomeGlyph, payload.LeadId, payload.Outcome, evt.OccurredAtUtc, valueLabel, lossReason));
            }
        }

        if (!anyDeals)
        {
            sb.AppendLine();
            sb.AppendLine("*No deals closed in this contest window.*");
        }

        var section = new ReplaySection(Kind, "Persona Deal Logs", sb.ToString().TrimEnd());
        return Task.FromResult(new SectionResult(section, Array.Empty<ReplayHighlight>()));
    }
}
