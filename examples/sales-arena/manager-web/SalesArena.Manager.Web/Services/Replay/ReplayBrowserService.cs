using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay;

namespace SalesArena.Manager.Web.Services.Replay;

public sealed class ReplayBrowserService : IReplayBrowserService
{
    private readonly IArenaLedger _ledger;
    private readonly IReplayGenerator _generator;

    public ReplayBrowserService(IArenaLedger ledger, IReplayGenerator generator)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    public async Task<IReadOnlyList<ReplayContestSummary>> ListContestsAsync(CancellationToken cancellationToken = default)
    {
        var aggregates = new Dictionary<string, ContestAggregate>(StringComparer.Ordinal);

        await foreach (var evt in _ledger.QueryAsync(new ArenaEventFilter(), cancellationToken).ConfigureAwait(false))
        {
            if (!aggregates.TryGetValue(evt.ContestId, out var agg))
            {
                agg = new ContestAggregate(evt.ContestId);
                aggregates[evt.ContestId] = agg;
            }

            agg.Observe(evt);
        }

        return aggregates.Values
            .Select(a => a.ToSummary())
            .OrderByDescending(s => s.EndedAtUtc)
            .ToList();
    }

    public Task<ReplayReport> GetReportAsync(string contestId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contestId);
        return _generator.GenerateAsync(
            new ReplayOptions(
                contestId,
                new RevenueScoring(),
                ContestDisplayName: FormatDisplayName(contestId)),
            cancellationToken);
    }

    public async Task<ReplayDealFocus?> GetDealFocusAsync(
        string contestId,
        string leadId,
        ReplayReport? report = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leadId);

        var timeline = new List<ReplayDealEvent>();
        string? persona = null;

        await foreach (var evt in _ledger.QueryAsync(
                           ArenaEventFilter.ForLead(contestId, leadId),
                           cancellationToken).ConfigureAwait(false))
        {
            persona ??= evt.Persona;
            var summary = SummarizeEvent(evt);
            if (summary is not null)
            {
                timeline.Add(new ReplayDealEvent(evt.OccurredAtUtc, evt.Kind, summary));
            }
        }

        if (timeline.Count == 0)
        {
            return null;
        }

        report ??= await GetReportAsync(contestId, cancellationToken).ConfigureAwait(false);
        var highlights = ReplayBrowserQuery.HighlightsForLead(report.Highlights, leadId);

        return new ReplayDealFocus(leadId, persona, timeline, highlights);
    }

    internal static string FormatDisplayName(string contestId) =>
        contestId.Replace("-", " ", StringComparison.Ordinal);

    private static string? SummarizeEvent(ArenaEvent evt) => evt.Kind switch
    {
        ArenaEventKinds.TouchSent => FormatTouch(evt.GetPayload<TouchSentPayload>()),
        ArenaEventKinds.InboundReceived => FormatInbound(evt.GetPayload<InboundReceivedPayload>()),
        ArenaEventKinds.DealClosed => FormatDeal(evt.GetPayload<DealClosedPayload>()),
        ArenaEventKinds.ProposalSent => FormatProposal(evt.GetPayload<ProposalSentPayload>()),
        ArenaEventKinds.MeetingBooked => FormatMeetingBooked(evt.GetPayload<MeetingBookedPayload>()),
        ArenaEventKinds.MeetingHeld => FormatMeetingHeld(evt.GetPayload<MeetingHeldPayload>()),
        ArenaEventKinds.LeadAssigned => FormatLeadAssigned(evt.GetPayload<LeadAssignedPayload>()),
        _ => null,
    };

    private static string? FormatTouch(TouchSentPayload? p) =>
        p is null ? null : $"{p.Persona} sent {p.Channel} ({p.TemplateId}/{p.VariantId})";

    private static string? FormatInbound(InboundReceivedPayload? p) =>
        p is null ? null : $"Inbound {p.Channel}: {p.Intent} ({p.Sentiment})";

    private static string? FormatDeal(DealClosedPayload? p) =>
        p is null ? null : $"Deal {p.Outcome}" + (p.ValueUsd is { } v ? $" ${v:N0}" : string.Empty);

    private static string? FormatProposal(ProposalSentPayload? p) =>
        p is null ? null : $"Proposal {p.PricingTier} (${p.TotalContractValueUsd:N0})";

    private static string? FormatMeetingBooked(MeetingBookedPayload? p) =>
        p is null ? null : $"Meeting booked for {p.ScheduledForUtc:u}";

    private static string? FormatMeetingHeld(MeetingHeldPayload? p) =>
        p is null ? null : $"Meeting held ({p.DurationMinutes} min)";

    private static string? FormatLeadAssigned(LeadAssignedPayload? p) =>
        p is null ? null : $"Lead assigned to {p.Persona} via {p.Source}";

    private sealed class ContestAggregate
    {
        private DateTimeOffset _lastUtc = DateTimeOffset.MinValue;
        private string? _displayName;
        private string? _winningPersona;
        private int _dealCount;

        public ContestAggregate(string contestId) => ContestId = contestId;

        public string ContestId { get; }

        public void Observe(ArenaEvent evt)
        {
            if (evt.OccurredAtUtc > _lastUtc)
            {
                _lastUtc = evt.OccurredAtUtc;
            }

            if (evt.Kind == ArenaEventKinds.ContestPhaseChanged)
            {
                var phase = evt.GetPayload<ContestPhaseChangedPayload>();
                if (phase?.Reason is { Length: > 0 } reason && !reason.Contains("bootstrap", StringComparison.OrdinalIgnoreCase))
                {
                    _displayName = reason;
                }
            }

            if (evt.Kind == ArenaEventKinds.LeaderboardSnapshot)
            {
                var snap = evt.GetPayload<LeaderboardSnapshotPayload>();
                var top = snap?.Entries.OrderBy(e => e.Position).FirstOrDefault();
                if (top is not null)
                {
                    _winningPersona = top.Persona;
                }
            }

            if (evt.Kind == ArenaEventKinds.DealClosed)
            {
                _dealCount++;
            }
        }

        public ReplayContestSummary ToSummary() =>
            new(
                ContestId,
                _displayName ?? FormatDisplayName(ContestId),
                _lastUtc == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : _lastUtc,
                _winningPersona,
                _dealCount);
    }
}
