using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Training.Diary;
using Xunit;

namespace SalesArena.Training.Tests;

public sealed class StubDiaryWriterTests
{
    private const string Contest = "diary-test";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WriteEntry_hits_word_count_floor_for_busy_day()
    {
        var writer = new StubDiaryWriter();
        var ctx = new DiaryDayContext(
            Persona: "roma",
            Day: 1,
            LeaderboardPosition: 1,
            TotalPositions: 5,
            DealsClosedToday: 2,
            DealsLostToday: 1,
            RevenueToday: 50_000m,
            Events: new[] { NewWin(1, 50_000m), NewWin(2, 25_000m), NewLoss(3) });

        var body = await writer.WriteEntryAsync(ctx);
        var words = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        words.Should().BeGreaterThanOrEqualTo(120);
        body.Should().Contain("[evt:1]");
        body.Should().Contain("[evt:2]");
    }

    [Fact]
    public async Task WriteEntry_picks_soaring_tone_for_top_third_position()
    {
        var writer = new StubDiaryWriter();
        var body = await writer.WriteEntryAsync(NewCtx(persona: "roma", pos: 1, total: 6, win: true));

        body.Should().Contain("Day 1 — roma");
        body.Should().Contain("board likes me today");  // soaring opening fragment
    }

    [Fact]
    public async Task WriteEntry_picks_steady_tone_for_middle_position()
    {
        var writer = new StubDiaryWriter();
        var body = await writer.WriteEntryAsync(NewCtx(persona: "moss", pos: 4, total: 6, win: true));

        body.Should().Contain("Holding at #4");
        body.Should().Contain("ledger doesn't lie");
    }

    [Fact]
    public async Task WriteEntry_picks_bruised_tone_for_bottom_third()
    {
        var writer = new StubDiaryWriter();
        var body = await writer.WriteEntryAsync(NewCtx(persona: "levene", pos: 6, total: 6, win: false));

        body.Should().Contain("Position #6 hurts");
        body.Should().Contain("most afraid of");
    }

    [Fact]
    public async Task WriteEntry_for_empty_day_uses_none_sentinels()
    {
        var writer = new StubDiaryWriter();
        var ctx = new DiaryDayContext("aaronow", 3, 3, 4, 0, 0, 0m, Array.Empty<ArenaEvent>());

        var body = await writer.WriteEntryAsync(ctx);

        body.Should().Contain("[evt:none]");
        // Even quiet days should hit the floor via padding.
        body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length.Should().BeGreaterThanOrEqualTo(60);
    }

    [Fact]
    public async Task WriteEntry_prefers_dealClosed_won_over_other_kinds()
    {
        var writer = new StubDiaryWriter();
        var ctx = new DiaryDayContext(
            Persona: "roma",
            Day: 1,
            LeaderboardPosition: 1,
            TotalPositions: 5,
            DealsClosedToday: 1,
            DealsLostToday: 0,
            RevenueToday: 100_000m,
            Events: new[]
            {
                NewKind(10, ArenaEventKinds.TouchSent),
                NewKind(11, ArenaEventKinds.MeetingBooked),
                NewWin(12, 100_000m),
            });

        var body = await writer.WriteEntryAsync(ctx);

        // Win should be anchored first; touch + meeting fill the next two slots.
        body.Should().Contain("[evt:12]");
        body.Should().Contain("the bell rang");  // soaring tone for won deal
    }

    // ---- helpers --------------------------------------------------------

    private static DiaryDayContext NewCtx(string persona, int pos, int total, bool win)
    {
        var events = win
            ? (IReadOnlyList<ArenaEvent>)new[] { NewWin(1, 50_000m), NewWin(2, 25_000m) }
            : new[] { NewLoss(1), NewLoss(2) };
        return new DiaryDayContext(persona, 1, pos, total, win ? 2 : 0, win ? 0 : 2, win ? 75_000m : 0m, events);
    }

    private static ArenaEvent NewWin(long id, decimal value) => new()
    {
        Id = id,
        ContestId = Contest,
        Kind = ArenaEventKinds.DealClosed,
        OccurredAtUtc = T0.AddHours(id),
        LeadId = $"L-{id}",
        Persona = "p",
        PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload($"L-{id}", "p", "Won", value, null)),
    };

    private static ArenaEvent NewLoss(long id) => new()
    {
        Id = id,
        ContestId = Contest,
        Kind = ArenaEventKinds.DealClosed,
        OccurredAtUtc = T0.AddHours(id),
        LeadId = $"L-{id}",
        Persona = "p",
        PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload($"L-{id}", "p", "Lost", 0m, "price")),
    };

    private static ArenaEvent NewKind(long id, string kind) => new()
    {
        Id = id,
        ContestId = Contest,
        Kind = kind,
        OccurredAtUtc = T0.AddHours(id),
        LeadId = $"L-{id}",
        Persona = "p",
        PayloadJson = "{}",
    };
}
