using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Training.Diary;
using Xunit;

namespace SalesArena.Training.Tests;

public sealed class DiaryGeneratorTests
{
    private const string Contest = "Tuesday-Diary";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateAsync_persists_entry_with_frontmatter_and_word_count()
    {
        await using var ledger = await SeedDayAsync();
        var store = new InMemoryDiaryStore();
        var generator = new DiaryGenerator(ledger, store: store, time: new FakeTime(T0.AddHours(24)));

        var entry = await generator.GenerateAsync(NewRequest(persona: "roma", day: 1, position: 1));

        entry.Persona.Should().Be("roma");
        entry.Day.Should().Be(1);
        entry.WordCount.Should().BeGreaterThanOrEqualTo(120);
        entry.CitedEventIds.Should().HaveCountGreaterThanOrEqualTo(2);
        entry.Markdown.Should().StartWith("---");
        entry.Markdown.Should().Contain("contest: Tuesday-Diary");
        entry.Markdown.Should().Contain("persona: roma");
        entry.Markdown.Should().Contain("# roma — Day 1");
        entry.Markdown.Should().Contain("[evt:");

        // Persisted to the store.
        var loaded = await store.LoadAsync(Contest, "roma");
        loaded.Should().HaveCount(1);
        loaded[0].Day.Should().Be(1);
    }

    [Fact]
    public async Task GenerateAsync_throws_when_writer_output_fails_guard()
    {
        await using var ledger = await SeedDayAsync();
        var generator = new DiaryGenerator(
            ledger,
            writer: new BadWriter(),
            time: new FakeTime(T0.AddHours(24)));

        var act = async () => await generator.GenerateAsync(NewRequest("roma", 1, 1));

        (await act.Should().ThrowAsync<DiaryGuardException>())
            .Which.Result.IsOk.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateAsync_filters_events_to_the_day_window_only()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        // Day 1: 9am close. Day 2: 10am close. Diary for Day 1 must NOT see day 2.
        await ledger.AppendAsync(NewWin("roma", "L-D1", 50_000m, T0.AddHours(9)));
        await ledger.AppendAsync(NewWin("roma", "L-D2", 80_000m, T0.AddDays(1).AddHours(10)));
        await ledger.AppendAsync(NewWin("roma", "L-D1b", 20_000m, T0.AddHours(11)));

        var generator = new DiaryGenerator(ledger, time: new FakeTime(T0.AddHours(24)));

        var entry = await generator.GenerateAsync(new DiaryGenerationRequest(
            ContestId: Contest,
            Persona: "roma",
            Day: 1,
            DayStartUtc: T0,
            DayEndUtc: T0.AddDays(1),
            LeaderboardPosition: 1,
            TotalPositions: 4));

        // Day 1: 2 wins, $70K; day 2 close not counted.
        entry.Markdown.Should().Contain("$70,000");
        entry.Markdown.Should().NotContain("L-D2");
    }

    [Fact]
    public async Task GenerateAsync_with_no_events_produces_quiet_day_entry()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var generator = new DiaryGenerator(ledger, time: new FakeTime(T0.AddHours(24)));

        var entry = await generator.GenerateAsync(NewRequest("aaronow", 1, 3));

        entry.Markdown.Should().Contain("Quiet day");
        entry.Markdown.Should().Contain("[evt:none]");
        entry.WordCount.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task GenerateAsync_persona_arc_loads_in_day_order()
    {
        await using var ledger = await SeedDayAsync();
        // Add a 2nd day's win.
        await ledger.AppendAsync(NewWin("roma", "L-D2", 30_000m, T0.AddDays(1).AddHours(2)));
        await ledger.AppendAsync(NewWin("roma", "L-D2b", 15_000m, T0.AddDays(1).AddHours(5)));

        var store = new InMemoryDiaryStore();
        var generator = new DiaryGenerator(ledger, store: store, time: new FakeTime(T0.AddHours(48)));

        await generator.GenerateAsync(NewRequest("roma", day: 1, position: 1));
        await generator.GenerateAsync(new DiaryGenerationRequest(
            Contest, "roma", 2, T0.AddDays(1), T0.AddDays(2), 1, 4));

        var arc = await store.LoadAsync(Contest, "roma");
        arc.Select(e => e.Day).Should().Equal(new[] { 1, 2 });
    }

    [Fact]
    public async Task FileSystemDiaryStore_writes_markdown_to_expected_path()
    {
        var root = Path.Combine(Path.GetTempPath(), $"diary-{Guid.NewGuid():N}");
        try
        {
            await using var ledger = await SeedDayAsync();
            var store = new FileSystemDiaryStore(root);
            var generator = new DiaryGenerator(ledger, store: store, time: new FakeTime(T0.AddHours(24)));

            var entry = await generator.GenerateAsync(NewRequest("roma", 1, 1));

            var expected = Path.Combine(root, Contest, "roma", "001.md");
            File.Exists(expected).Should().BeTrue();
            (await File.ReadAllTextAsync(expected)).Should().Be(entry.Markdown);

            var arc = await store.LoadAsync(Contest, "roma");
            arc.Should().HaveCount(1);
            arc[0].Markdown.Should().Be(entry.Markdown);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task FileSystemDiaryStore_rejects_path_traversal_segments()
    {
        var root = Path.Combine(Path.GetTempPath(), $"diary-{Guid.NewGuid():N}");
        try
        {
            var store = new FileSystemDiaryStore(root);

            // '..' as a segment is refused outright (ArgumentException) — even
            // pre-sanitization, the canonical form would equal '..' and we
            // forbid that. Defense-in-depth against path traversal.
            var traversalEntry = new DiaryEntry("..", "good-persona", 1, T0, 1, 100, Array.Empty<string>(), "body");
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(traversalEntry));

            // Less obvious traversal characters (slashes, drive letters) get
            // sanitized to underscores so nothing escapes the root.
            var sanitizedEntry = new DiaryEntry("contest/with:slash", "persona\\back", 1, T0, 1, 100, Array.Empty<string>(), "body");
            var path = await store.SaveAsync(sanitizedEntry);
            path.Should().StartWith(root);
            path.Should().NotContain(":");
            path.Should().NotContain("..");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAsync_rejects_inverted_day_window()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var generator = new DiaryGenerator(ledger);

        var act = async () => await generator.GenerateAsync(new DiaryGenerationRequest(
            ContestId: Contest,
            Persona: "roma",
            Day: 1,
            DayStartUtc: T0.AddDays(1),
            DayEndUtc: T0,
            LeaderboardPosition: 1,
            TotalPositions: 4));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- helpers --------------------------------------------------------

    private static async Task<SqliteArenaLedger> SeedDayAsync()
    {
        var ledger = new SqliteArenaLedger("Data Source=:memory:");
        await ledger.AppendAsync(NewWin("roma", "L-101", 60_000m, T0.AddHours(2)));
        await ledger.AppendAsync(NewWin("roma", "L-102", 40_000m, T0.AddHours(5)));
        await ledger.AppendAsync(NewLoss("roma", "L-103", T0.AddHours(7)));
        return ledger;
    }

    private static ArenaEvent NewWin(string persona, string leadId, decimal value, DateTimeOffset at) => new()
    {
        ContestId = Contest,
        Kind = ArenaEventKinds.DealClosed,
        OccurredAtUtc = at,
        LeadId = leadId,
        Persona = persona,
        PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, "Won", value, null)),
    };

    private static ArenaEvent NewLoss(string persona, string leadId, DateTimeOffset at) => new()
    {
        ContestId = Contest,
        Kind = ArenaEventKinds.DealClosed,
        OccurredAtUtc = at,
        LeadId = leadId,
        Persona = persona,
        PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, "Lost", 0m, "no-budget")),
    };

    private static DiaryGenerationRequest NewRequest(string persona, int day, int position) =>
        new(Contest, persona, day, T0, T0.AddDays(1), position, 4);

    private sealed class BadWriter : IDiaryWriter
    {
        public Task<string> WriteEntryAsync(DiaryDayContext day, CancellationToken cancellationToken = default) =>
            Task.FromResult("No specifics, no citations, just vibes.");
    }

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
