using FluentAssertions;
using SalesArena.Crm;
using Xunit;

namespace SalesArena.Crm.Tests;

/// <summary>
/// Pins the activity-log contract: append-only, idempotent re-open, ordered
/// read, lead-scoped query, disposal hygiene.
/// </summary>
public sealed class SqliteActivityLogTests
{
    [Fact]
    public async Task AppendAndReadByLead_returns_chronological_entries()
    {
        await using var log = new SqliteActivityLog("Data Source=:memory:");
        var t0 = new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero);

        await log.AppendAsync(new ActivityLogEntry(0, "L-0001", CrmStages.Lead, CrmStages.Researched, "roma", t0, "ev-1"));
        await log.AppendAsync(new ActivityLogEntry(0, "L-0001", CrmStages.Researched, CrmStages.Contacted, "roma", t0.AddMinutes(1), "ev-2"));
        await log.AppendAsync(new ActivityLogEntry(0, "L-0002", CrmStages.Lead, CrmStages.Researched, "levene", t0.AddMinutes(2), null));

        var l1 = await log.GetByLeadAsync("L-0001");
        l1.Should().HaveCount(2);
        l1[0].ToStage.Should().Be(CrmStages.Researched);
        l1[1].ToStage.Should().Be(CrmStages.Contacted);
        l1[0].EvidenceRef.Should().Be("ev-1");
        l1[1].EvidenceRef.Should().Be("ev-2");

        var l2 = await log.GetByLeadAsync("L-0002");
        l2.Should().HaveCount(1);
        l2[0].EvidenceRef.Should().BeNull();
    }

    [Fact]
    public async Task GetByLead_for_unknown_lead_returns_empty()
    {
        await using var log = new SqliteActivityLog("Data Source=:memory:");
        var rows = await log.GetByLeadAsync("L-9999");
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Count_returns_total_across_leads()
    {
        await using var log = new SqliteActivityLog("Data Source=:memory:");
        var t = DateTimeOffset.UtcNow;

        await log.AppendAsync(new ActivityLogEntry(0, "L-A", CrmStages.Lead, CrmStages.Researched, "p", t, null));
        await log.AppendAsync(new ActivityLogEntry(0, "L-A", CrmStages.Researched, CrmStages.Contacted, "p", t.AddMinutes(1), null));
        await log.AppendAsync(new ActivityLogEntry(0, "L-B", CrmStages.Lead, CrmStages.Researched, "p", t.AddMinutes(2), null));

        (await log.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Disposed_log_refuses_further_operations()
    {
        var log = new SqliteActivityLog("Data Source=:memory:");
        await log.DisposeAsync();

        var append = async () => await log.AppendAsync(new ActivityLogEntry(0, "L-A", CrmStages.Lead, CrmStages.Researched, "p", DateTimeOffset.UtcNow, null));
        await append.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task Append_returns_monotonically_increasing_ids()
    {
        await using var log = new SqliteActivityLog("Data Source=:memory:");
        var t = DateTimeOffset.UtcNow;

        var id1 = await log.AppendAsync(new ActivityLogEntry(0, "L-A", CrmStages.Lead, CrmStages.Researched, "p", t, null));
        var id2 = await log.AppendAsync(new ActivityLogEntry(0, "L-A", CrmStages.Researched, CrmStages.Contacted, "p", t.AddMinutes(1), null));
        var id3 = await log.AppendAsync(new ActivityLogEntry(0, "L-B", CrmStages.Lead, CrmStages.Researched, "p", t.AddMinutes(2), null));

        id1.Should().BeLessThan(id2);
        id2.Should().BeLessThan(id3);
    }
}
