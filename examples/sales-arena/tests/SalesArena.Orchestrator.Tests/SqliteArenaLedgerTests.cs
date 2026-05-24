using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Orchestrator.Tests;

/// <summary>
/// Pins the ledger contract: append-only, indexed filtering, typed payload
/// round-trip, transactional batch, disposal hygiene, validation.
/// </summary>
public sealed class SqliteArenaLedgerTests
{
    private const string Contest = "Tuesday-Steak-Knives";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AppendAsync_assigns_monotonic_id()
    {
        await using var ledger = New();
        var a = await ledger.AppendAsync(NewEvent(ArenaEventKinds.LeadAssigned, "L-0001", "roma", T0));
        var b = await ledger.AppendAsync(NewEvent(ArenaEventKinds.TouchSent, "L-0001", "roma", T0.AddMinutes(1)));
        var c = await ledger.AppendAsync(NewEvent(ArenaEventKinds.DealClosed, "L-0001", "roma", T0.AddMinutes(2)));

        a.Id.Should().BeGreaterThan(0);
        b.Id.Should().BeGreaterThan(a.Id);
        c.Id.Should().BeGreaterThan(b.Id);
    }

    [Fact]
    public async Task QueryAsync_filters_by_contest_lead_persona_and_kind()
    {
        await using var ledger = New();
        // 3 events in our contest, 1 noise event in a different contest
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.LeadAssigned, "L-0001", "roma", T0));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.TouchSent, "L-0001", "roma", T0.AddMinutes(1)));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.DealClosed, "L-0001", "roma", T0.AddMinutes(2)));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.DealClosed, "L-9999", "levene", T0.AddMinutes(3), contestId: "Other"));

        var byContest = await ledger.QueryAsync(ArenaEventFilter.ForContest(Contest)).ToListAsync();
        byContest.Should().HaveCount(3);

        var byLead = await ledger.QueryAsync(ArenaEventFilter.ForLead(Contest, "L-0001")).ToListAsync();
        byLead.Should().HaveCount(3);

        var byPersona = await ledger.QueryAsync(ArenaEventFilter.ForPersona(Contest, "roma")).ToListAsync();
        byPersona.Should().HaveCount(3);

        var byKind = await ledger.QueryAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.DealClosed)).ToListAsync();
        byKind.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_default_ordering_is_chronological_ascending()
    {
        await using var ledger = New();
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.TouchSent, "L-0001", "roma", T0.AddMinutes(5)));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.LeadAssigned, "L-0001", "roma", T0));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.DealClosed, "L-0001", "roma", T0.AddMinutes(10)));

        var rows = await ledger.QueryAsync(ArenaEventFilter.ForContest(Contest)).ToListAsync();
        rows.Select(e => e.Kind).Should().Equal(new[]
        {
            ArenaEventKinds.LeadAssigned,
            ArenaEventKinds.TouchSent,
            ArenaEventKinds.DealClosed,
        });
    }

    [Fact]
    public async Task QueryAsync_descending_time_reverses_order()
    {
        await using var ledger = New();
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.LeadAssigned, "L-0001", "roma", T0));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.TouchSent, "L-0001", "roma", T0.AddMinutes(5)));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.DealClosed, "L-0001", "roma", T0.AddMinutes(10)));

        var rows = await ledger.QueryAsync(
            ArenaEventFilter.ForContest(Contest) with { DescendingTime = true }).ToListAsync();
        rows.Select(e => e.Kind).Should().Equal(new[]
        {
            ArenaEventKinds.DealClosed,
            ArenaEventKinds.TouchSent,
            ArenaEventKinds.LeadAssigned,
        });
    }

    [Fact]
    public async Task QueryAsync_respects_time_range_and_limit()
    {
        await using var ledger = New();
        for (var i = 0; i < 10; i++)
        {
            await ledger.AppendAsync(NewEvent(ArenaEventKinds.TouchSent, $"L-{i:0000}", "roma", T0.AddMinutes(i)));
        }

        var windowed = await ledger.QueryAsync(
            new ArenaEventFilter(ContestId: Contest, FromUtc: T0.AddMinutes(3), ToUtc: T0.AddMinutes(6))).ToListAsync();
        windowed.Should().HaveCount(4);   // minutes 3, 4, 5, 6 inclusive

        var top3 = await ledger.QueryAsync(new ArenaEventFilter(ContestId: Contest, Limit: 3)).ToListAsync();
        top3.Should().HaveCount(3);
    }

    [Fact]
    public async Task CountAsync_matches_query_count_with_same_filter()
    {
        await using var ledger = New();
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.LeadAssigned, "L-0001", "roma", T0));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.TouchSent, "L-0001", "roma", T0.AddMinutes(1)));
        await ledger.AppendAsync(NewEvent(ArenaEventKinds.DealClosed, "L-0002", "levene", T0.AddMinutes(2)));

        (await ledger.CountAsync(ArenaEventFilter.ForContest(Contest))).Should().Be(3);
        (await ledger.CountAsync(ArenaEventFilter.ForPersona(Contest, "roma"))).Should().Be(2);
        (await ledger.CountAsync(ArenaEventFilter.ForLead(Contest, "L-0002"))).Should().Be(1);
        (await ledger.CountAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.DealClosed))).Should().Be(1);
    }

    [Fact]
    public async Task TypedPayload_round_trips_through_JSON()
    {
        await using var ledger = New();
        var payload = new DealClosedPayload(
            LeadId: "L-0001",
            Persona: "roma",
            Outcome: "Won",
            ValueUsd: 48_000m,
            LossReason: null);

        var saved = await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = Contest,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = T0,
            LeadId = "L-0001",
            Persona = "roma",
            PayloadJson = ArenaEvent.SerializePayload(payload),
        });

        var fetched = (await ledger.QueryAsync(ArenaEventFilter.ForLead(Contest, "L-0001")).ToListAsync()).Single();
        var roundtripped = fetched.GetPayload<DealClosedPayload>();
        roundtripped.Should().BeEquivalentTo(payload);
    }

    [Fact]
    public async Task TypedPayload_with_list_of_strings_round_trips()
    {
        await using var ledger = New();
        var payload = new GlengarryLeadDrippedPayload(
            Persona: "roma",
            LeadIds: new[] { "L-0001", "L-0007", "L-0019" },
            Reason: "tier-1-window-snapshot-2026-05-18T15:00:00Z");

        await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = Contest,
            Kind = ArenaEventKinds.GlengarryLeadDripped,
            OccurredAtUtc = T0,
            Persona = "roma",
            PayloadJson = ArenaEvent.SerializePayload(payload),
        });

        var fetched = (await ledger.QueryAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.GlengarryLeadDripped)).ToListAsync()).Single();
        var roundtripped = fetched.GetPayload<GlengarryLeadDrippedPayload>()!;
        roundtripped.Persona.Should().Be("roma");
        roundtripped.LeadIds.Should().BeEquivalentTo(new[] { "L-0001", "L-0007", "L-0019" });
    }

    [Fact]
    public async Task AppendManyAsync_persists_atomically_and_assigns_ids()
    {
        await using var ledger = New();
        var batch = new[]
        {
            NewEvent(ArenaEventKinds.LeadAssigned, "L-A", "roma", T0),
            NewEvent(ArenaEventKinds.LeadAssigned, "L-B", "roma", T0.AddSeconds(1)),
            NewEvent(ArenaEventKinds.LeadAssigned, "L-C", "roma", T0.AddSeconds(2)),
        };

        var saved = await ledger.AppendManyAsync(batch);
        saved.Should().HaveCount(3);
        saved.Select(e => e.Id).Should().OnlyHaveUniqueItems();
        (await ledger.CountAsync(ArenaEventFilter.ForContest(Contest))).Should().Be(3);
    }

    [Fact]
    public async Task AppendAsync_rejects_unknown_event_kind()
    {
        await using var ledger = New();
        var act = async () => await ledger.AppendAsync(NewEvent("VibeShift", "L-0001", "roma", T0));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AppendAsync_rejects_blank_contestId_or_kind()
    {
        await using var ledger = New();
        var noContest = async () => await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = "",
            Kind = ArenaEventKinds.LeadAssigned,
            OccurredAtUtc = T0,
            PayloadJson = "{}",
        });
        await noContest.Should().ThrowAsync<ArgumentException>();

        var noKind = async () => await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = Contest,
            Kind = "",
            OccurredAtUtc = T0,
            PayloadJson = "{}",
        });
        await noKind.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Disposed_ledger_refuses_further_operations()
    {
        var ledger = New();
        await ledger.DisposeAsync();

        var append = async () => await ledger.AppendAsync(NewEvent(ArenaEventKinds.LeadAssigned, "L-A", "roma", T0));
        await append.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ReopenedLedger_keeps_previously_appended_events()
    {
        // Use a temp file so the second connection can read the same DB.
        var path = Path.GetTempFileName();
        try
        {
            await using (var first = new SqliteArenaLedger($"Data Source={path}"))
            {
                await first.AppendAsync(NewEvent(ArenaEventKinds.LeadAssigned, "L-0001", "roma", T0));
                await first.AppendAsync(NewEvent(ArenaEventKinds.DealClosed, "L-0001", "roma", T0.AddMinutes(5)));
            }

            await using var second = new SqliteArenaLedger($"Data Source={path}");
            var rows = await second.QueryAsync(ArenaEventFilter.ForContest(Contest)).ToListAsync();
            rows.Should().HaveCount(2);
        }
        finally
        {
            try { File.Delete(path); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void ArenaEventKinds_All_contains_every_declared_kind()
    {
        // Coverage check: every const string in ArenaEventKinds is in the All set.
        var consts = typeof(ArenaEventKinds).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();
        ArenaEventKinds.All.Should().BeEquivalentTo(consts);
        consts.Should().Contain(ArenaEventKinds.LeadAssigned);
        consts.Should().Contain(ArenaEventKinds.DealClosed);
        consts.Should().Contain(ArenaEventKinds.BellRung);
        consts.Count.Should().Be(15);
    }

    // ---- helpers ---------------------------------------------------------

    private static SqliteArenaLedger New() => new("Data Source=:memory:");

    private static ArenaEvent NewEvent(string kind, string leadId, string persona, DateTimeOffset at, string contestId = Contest) => new()
    {
        ContestId = contestId,
        Kind = kind,
        OccurredAtUtc = at,
        LeadId = leadId,
        Persona = persona,
        PayloadJson = "{}",
    };
}

internal static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source) list.Add(item);
        return list;
    }
}
