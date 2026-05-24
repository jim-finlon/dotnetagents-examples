using SalesArena.Manager.Web.Services.ChannelPivot;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class ChannelPivotServiceTests
{
    [Fact]
    public async Task BuildSnapshot_aggregates_reply_and_close_rates_by_channel_and_industry()
    {
        var ledger = new SqliteArenaLedger($"Data Source=pivot-test-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        const string contestId = "pivot-test";
        var t0 = DateTimeOffset.UtcNow;

        await ledger.AppendManyAsync(
        [
            Touch(contestId, "roma", "L-1000", t0, "email"),
            Touch(contestId, "roma", "L-1000", t0.AddSeconds(1), "email"),
            Inbound(contestId, "roma", "L-1000", t0.AddSeconds(2), "email"),
            Touch(contestId, "levene", "L-1001", t0.AddSeconds(3), "sms"),
            Inbound(contestId, "levene", "L-1001", t0.AddSeconds(4), "sms"),
            Deal(contestId, "levene", "L-1001", t0.AddSeconds(5)),
        ]);

        var service = new ChannelPivotService(ledger);
        var snapshot = await service.BuildSnapshotAsync(contestId);

        var emailPharma = snapshot.Cells.Single(c =>
            c.Channel == "email" && c.Industry == "pharma");
        Assert.Equal(2, emailPharma.TouchCount);
        Assert.Equal(1, emailPharma.InboundCount);
        Assert.Equal(0, emailPharma.CloseCount);
        Assert.Equal(0.5, emailPharma.ReplyRate, 3);
        Assert.True(emailPharma.LowSample);

        var smsSaas = snapshot.Cells.Single(c =>
            c.Channel == "sms" && c.Industry == "saas");
        Assert.Equal(1, smsSaas.TouchCount);
        Assert.Equal(1, smsSaas.InboundCount);
        Assert.Equal(1, smsSaas.CloseCount);
        Assert.Equal(1.0, smsSaas.ReplyRate, 3);
        Assert.Equal(1.0, smsSaas.CloseRate, 3);
        Assert.True(smsSaas.LowSample);
    }

    [Fact]
    public async Task ExportCsv_roundtrips_persona_rows_and_metric_mode()
    {
        var ledger = new SqliteArenaLedger($"Data Source=pivot-csv-{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
        const string contestId = "csv-test";
        var t0 = DateTimeOffset.UtcNow;

        await ledger.AppendManyAsync(
        [
            Touch(contestId, "roma", "L-1002", t0, "linkedin"),
            Inbound(contestId, "roma", "L-1002", t0.AddSeconds(1), "linkedin"),
        ]);

        var service = new ChannelPivotService(ledger);
        var snapshot = await service.BuildSnapshotAsync(contestId);
        var csv = service.ExportCsv(snapshot, ChannelPivotMetricMode.CloseRate);

        Assert.Contains("channel,industry,persona", csv, StringComparison.Ordinal);
        Assert.Contains("linkedin,retail,roma", csv, StringComparison.Ordinal);
        Assert.Contains("CloseRate", csv, StringComparison.Ordinal);
    }

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

    private static ArenaEvent Deal(string contestId, string persona, string leadId, DateTimeOffset at) =>
        new()
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = at,
            Persona = persona,
            LeadId = leadId,
            PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, "Won", 10_000m, null)),
        };
}
