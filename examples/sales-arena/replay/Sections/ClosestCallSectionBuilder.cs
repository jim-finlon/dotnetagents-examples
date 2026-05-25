using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.Sections;

/// <summary>
/// "The biggest deal that almost slipped." Picks the highest-value Lost
/// DealClosed event in the contest. Useful for the post-mortem: which
/// big ones got away?
/// </summary>
public sealed class ClosestCallSectionBuilder : ISectionBuilder
{
    public ReplaySectionKind Kind => ReplaySectionKind.ClosestCall;

    public Task<SectionResult> BuildAsync(SectionContext ctx, CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        sb.AppendLine(TemplateLoader.LoadHeader(Kind, ctx));
        sb.AppendLine();

        var lost = ctx.AllEvents
            .Where(e => e.Kind == ArenaEventKinds.DealClosed)
            .Select(e => (Event: e, Payload: e.GetPayload<DealClosedPayload>()))
            .Where(t => t.Payload is { } p && string.Equals(p.Outcome, "Lost", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (lost.Count == 0)
        {
            sb.AppendLine("*Every closed deal landed. Coffee is for everyone today.*");
            var emptyResult = new ReplaySection(Kind, "Closest Call", sb.ToString().TrimEnd());
            return Task.FromResult(new SectionResult(emptyResult, Array.Empty<ReplayHighlight>()));
        }

        // The "closest" call: by associated proposal value (if any) or by the most-touched-but-lost lead.
        var withProposalValue = JoinWithProposalValue(ctx.AllEvents, lost);
        var champion = withProposalValue
            .OrderByDescending(t => t.ProposalValueUsd ?? 0m)
            .ThenByDescending(t => CountTouches(ctx.AllEvents, t.LeadId, t.Persona))
            .First();

        var touchCount = CountTouches(ctx.AllEvents, champion.LeadId, champion.Persona);
        var amount = champion.ProposalValueUsd is { } pv
            ? string.Format(CultureInfo.InvariantCulture, "${0:N0}", pv)
            : "(no proposal value recorded)";

        sb.AppendLine($"- **{champion.LeadId}** worked by **{champion.Persona}**");
        sb.AppendLine($"- Lost on {champion.OccurredAtUtc:u} — proposal value at the time: {amount}");
        sb.AppendLine($"- Touch count before the loss: {touchCount}");
        if (!string.IsNullOrWhiteSpace(champion.LossReason))
        {
            sb.AppendLine($"- Reason recorded: *{champion.LossReason}*");
        }

        var highlight = new ReplayHighlight(
            Source: Kind,
            Headline: $"Closest call: {champion.Persona} lost {champion.LeadId} after {touchCount} touches.",
            Persona: champion.Persona,
            LeadId: champion.LeadId,
            ValueUsd: champion.ProposalValueUsd,
            OccurredAtUtc: champion.OccurredAtUtc);

        var section = new ReplaySection(Kind, "Closest Call", sb.ToString().TrimEnd());
        return Task.FromResult(new SectionResult(section, new[] { highlight }));
    }

    private static IEnumerable<LostCandidate> JoinWithProposalValue(
        IReadOnlyList<ArenaEvent> events,
        IEnumerable<(ArenaEvent Event, DealClosedPayload? Payload)> losses)
    {
        // Map: (leadId, persona) → latest ProposalSent value seen before the close.
        var proposals = events
            .Where(e => e.Kind == ArenaEventKinds.ProposalSent && !string.IsNullOrEmpty(e.LeadId))
            .Select(e => (Event: e, Payload: e.GetPayload<ProposalSentPayload>()))
            .Where(t => t.Payload is not null)
            .ToList();

        foreach (var (evt, payload) in losses)
        {
            if (payload is null) continue;
            var latestProposal = proposals
                .Where(p => p.Event.LeadId == evt.LeadId && p.Event.Persona == evt.Persona && p.Event.OccurredAtUtc <= evt.OccurredAtUtc)
                .OrderByDescending(p => p.Event.OccurredAtUtc)
                .FirstOrDefault();
            yield return new LostCandidate(
                LeadId: evt.LeadId ?? "?",
                Persona: evt.Persona ?? "?",
                OccurredAtUtc: evt.OccurredAtUtc,
                ProposalValueUsd: latestProposal.Payload?.TotalContractValueUsd ?? payload.ValueUsd,
                LossReason: payload.LossReason);
        }
    }

    private static int CountTouches(IReadOnlyList<ArenaEvent> events, string leadId, string persona) =>
        events.Count(e => e.Kind == ArenaEventKinds.TouchSent
                          && e.LeadId == leadId
                          && e.Persona == persona);

    private sealed record LostCandidate(string LeadId, string Persona, DateTimeOffset OccurredAtUtc, decimal? ProposalValueUsd, string? LossReason);
}
