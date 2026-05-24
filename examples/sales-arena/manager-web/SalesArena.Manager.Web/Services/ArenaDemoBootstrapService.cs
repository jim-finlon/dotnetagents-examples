using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Manager.Web.Services;

/// <summary>
/// Seeds demo ledger rows when empty so the Floor grid and ticker have live content on first run.
/// </summary>
public sealed class ArenaDemoBootstrapService : IHostedService
{
    private const string DemoContestId = "demo-contest";
    private const string ArchivedContestId = "archived-week-1";
    private readonly IArenaLedger _ledger;

    public ArenaDemoBootstrapService(IArenaLedger ledger) => _ledger = ledger;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (await _ledger.CountAsync(new ArenaEventFilter(), cancellationToken).ConfigureAwait(false) > 0)
        {
            return;
        }

        var t0 = DateTimeOffset.UtcNow;
        var archivedT0 = t0.AddDays(-7);
        await _ledger.AppendManyAsync(
        [
            new ArenaEvent
            {
                ContestId = DemoContestId,
                Kind = ArenaEventKinds.ContestPhaseChanged,
                OccurredAtUtc = t0,
                PayloadJson = ArenaEvent.SerializePayload(new ContestPhaseChangedPayload("Started", "Tuesday Steak-Knives Bake-Off")),
            },
            new ArenaEvent
            {
                ContestId = DemoContestId,
                Kind = ArenaEventKinds.LeaderboardSnapshot,
                OccurredAtUtc = t0.AddSeconds(1),
                PayloadJson = ArenaEvent.SerializePayload(new LeaderboardSnapshotPayload(
                [
                    new LeaderboardEntry("roma", 1, LeaderboardTierNames.Cadillac, 120_000m, 2, 0, 0.4),
                    new LeaderboardEntry("levene", 2, LeaderboardTierNames.SteakKnives, 45_000m, 1, 1, 0.25),
                    new LeaderboardEntry("moss", 3, LeaderboardTierNames.YouAreFired, 8_000m, 0, 2, 0.05),
                    new LeaderboardEntry("aaronow", 4, LeaderboardTierNames.SteakKnives, 22_000m, 1, 0, 0.2),
                ],
                "demo-scoring")),
            },
            Touch(DemoContestId, "roma", "L-1002", t0.AddSeconds(2), "email"),
            Touch(DemoContestId, "levene", "L-1003", t0.AddSeconds(3), "sms"),
            Touch(DemoContestId, "moss", "L-1004", t0.AddSeconds(3.5), "linkedin"),
            Touch(DemoContestId, "aaronow", "L-1005", t0.AddSeconds(3.6), "chat"),
            Inbound(DemoContestId, "roma", "L-1002", t0.AddSeconds(4), "email"),
            Inbound(DemoContestId, "levene", "L-1003", t0.AddSeconds(4.2), "sms"),
            Deal(DemoContestId, "roma", "L-1002", 75_000m, t0.AddSeconds(5)),
            Touch(DemoContestId, "roma", "L-1006", t0.AddSeconds(6), "email"),
            Inbound(DemoContestId, "roma", "L-1006", t0.AddSeconds(7), "email"),
            Touch(DemoContestId, "levene", "L-1007", t0.AddSeconds(8), "linkedin"),
            Inbound(DemoContestId, "levene", "L-1007", t0.AddSeconds(9), "linkedin"),
            Deal(DemoContestId, "levene", "L-1007", 22_000m, t0.AddSeconds(10)),
            new ArenaEvent
            {
                ContestId = ArchivedContestId,
                Kind = ArenaEventKinds.ContestPhaseChanged,
                OccurredAtUtc = archivedT0,
                PayloadJson = ArenaEvent.SerializePayload(new ContestPhaseChangedPayload("Ended", "Last week's Glengarry sprint")),
            },
            new ArenaEvent
            {
                ContestId = ArchivedContestId,
                Kind = ArenaEventKinds.LeaderboardSnapshot,
                OccurredAtUtc = archivedT0.AddMinutes(1),
                PayloadJson = ArenaEvent.SerializePayload(new LeaderboardSnapshotPayload(
                [
                    new LeaderboardEntry("moss", 1, LeaderboardTierNames.Cadillac, 88_000m, 1, 0, 0.35),
                    new LeaderboardEntry("levene", 2, LeaderboardTierNames.SteakKnives, 31_000m, 1, 1, 0.2),
                ],
                "demo-scoring")),
            },
            Touch(ArchivedContestId, "moss", "L-201", archivedT0.AddMinutes(2), "chat"),
            Deal(ArchivedContestId, "moss", "L-201", 42_000m, archivedT0.AddMinutes(5)),
        ],
        cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static ArenaEvent Touch(string contestId, string persona, string leadId, DateTimeOffset at, string channel) =>
        new()
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.TouchSent,
            OccurredAtUtc = at,
            Persona = persona,
            LeadId = leadId,
            PayloadJson = ArenaEvent.SerializePayload(new TouchSentPayload(leadId, persona, channel, "t1", "v1", "Hello", 120)),
        };

    private static ArenaEvent Inbound(string contestId, string persona, string leadId, DateTimeOffset at, string channel) =>
        new()
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.InboundReceived,
            OccurredAtUtc = at,
            Persona = persona,
            LeadId = leadId,
            PayloadJson = ArenaEvent.SerializePayload(new InboundReceivedPayload(leadId, persona, channel, "interested", "positive", "medium")),
        };

    private static ArenaEvent Deal(string contestId, string persona, string leadId, decimal value, DateTimeOffset at) =>
        new()
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = at,
            Persona = persona,
            LeadId = leadId,
            PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, "Won", value, null)),
        };
}
