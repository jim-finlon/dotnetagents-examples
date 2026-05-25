using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.Sections;

/// <summary>
/// "The single touch that flipped the most value." For each (lead, persona),
/// pair the most recent TouchSent that occurred BEFORE the won DealClosed.
/// Rank pairs by the won-deal's value. The highest-value pair is the MVP touch.
/// </summary>
public sealed class MvpTouchSectionBuilder : ISectionBuilder
{
    public ReplaySectionKind Kind => ReplaySectionKind.MvpTouch;

    public Task<SectionResult> BuildAsync(SectionContext ctx, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(TemplateLoader.LoadHeader(Kind, ctx));
        sb.AppendLine();

        var wins = ctx.AllEvents
            .Where(e => e.Kind == ArenaEventKinds.DealClosed)
            .Select(e => (Event: e, Payload: e.GetPayload<DealClosedPayload>()))
            .Where(t => t.Payload is { } p
                        && string.Equals(p.Outcome, "Won", StringComparison.OrdinalIgnoreCase)
                        && p.ValueUsd is not null
                        && p.ValueUsd > 0m)
            .ToList();

        if (wins.Count == 0)
        {
            sb.AppendLine("*No won-deal values recorded; cannot attribute an MVP touch.*");
            var emptyResult = new ReplaySection(Kind, "MVP Touch", sb.ToString().TrimEnd());
            return Task.FromResult(new SectionResult(emptyResult, Array.Empty<ReplayHighlight>()));
        }

        var touches = ctx.AllEvents
            .Where(e => e.Kind == ArenaEventKinds.TouchSent && !string.IsNullOrEmpty(e.LeadId))
            .Select(e => (Event: e, Payload: e.GetPayload<TouchSentPayload>()))
            .Where(t => t.Payload is not null)
            .ToList();

        (ArenaEvent Win, DealClosedPayload WinPayload, ArenaEvent? Touch, TouchSentPayload? TouchPayload)? best = null;
        foreach (var (winEvt, winPayload) in wins)
        {
            if (winPayload is null) continue;
            var lastTouch = touches
                .Where(t => t.Event.LeadId == winEvt.LeadId
                            && t.Event.Persona == winEvt.Persona
                            && t.Event.OccurredAtUtc <= winEvt.OccurredAtUtc)
                .OrderByDescending(t => t.Event.OccurredAtUtc)
                .FirstOrDefault();

            if (best is null || (winPayload.ValueUsd ?? 0m) > (best.Value.WinPayload.ValueUsd ?? 0m))
            {
                best = (winEvt, winPayload, lastTouch.Event, lastTouch.Payload);
            }
        }

        if (best is null)
        {
            sb.AppendLine("*No won deals matched against any prior touch.*");
            var emptyResult = new ReplaySection(Kind, "MVP Touch", sb.ToString().TrimEnd());
            return Task.FromResult(new SectionResult(emptyResult, Array.Empty<ReplayHighlight>()));
        }

        var win = best.Value;
        var amount = string.Format(CultureInfo.InvariantCulture, "${0:N0}", win.WinPayload.ValueUsd ?? 0m);
        sb.AppendLine($"- **Deal:** {win.Win.LeadId} ({win.Win.Persona}) closed for {amount} at {win.Win.OccurredAtUtc:u}.");
        if (win.Touch is not null && win.TouchPayload is not null)
        {
            sb.AppendLine($"- **MVP touch:** {win.TouchPayload.Channel} via template `{win.TouchPayload.TemplateId}` (variant `{win.TouchPayload.VariantId}`) sent at {win.Touch.OccurredAtUtc:u}.");
            if (!string.IsNullOrWhiteSpace(win.TouchPayload.Subject))
            {
                sb.AppendLine($"- **Subject:** *{win.TouchPayload.Subject}*");
            }
        }
        else
        {
            sb.AppendLine($"- *No prior touch on record for this lead — the close came inbound or off-channel.*");
        }

        var highlight = new ReplayHighlight(
            Source: Kind,
            Headline: $"MVP touch: {win.Win.Persona} closed {win.Win.LeadId} for {amount} via {win.TouchPayload?.Channel ?? "no recorded touch"}.",
            Persona: win.Win.Persona,
            LeadId: win.Win.LeadId,
            ValueUsd: win.WinPayload.ValueUsd,
            OccurredAtUtc: win.Win.OccurredAtUtc);

        var section = new ReplaySection(Kind, "MVP Touch", sb.ToString().TrimEnd());
        return Task.FromResult(new SectionResult(section, new[] { highlight }));
    }
}
