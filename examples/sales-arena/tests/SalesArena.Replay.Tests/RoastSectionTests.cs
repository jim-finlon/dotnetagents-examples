using System.Text.RegularExpressions;
using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay;
using SalesArena.Replay.Sections;
using SalesArena.Replay.Sections.Roast;
using Xunit;

namespace SalesArena.Replay.Tests;

/// <summary>
/// Pins the Roast section: hallucination guard refuses uncited / fake-id
/// output, voice-consistency across roaster personas, "no roast partner"
/// graceful path, custom IRoastWriter pluggability.
/// </summary>
public sealed class RoastSectionTests
{
    private const string Contest = "Tuesday-Roast-Off";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    // ---- Hallucination guard ---------------------------------------------

    [Fact]
    public void HallucinationGuard_passes_text_with_real_citations()
    {
        var events = new[] { NewLostDeal(1, "levene"), NewLostDeal(2, "levene") };
        var result = RoastHallucinationGuard.Verify("Levene lost [evt:1] and [evt:2].", events);

        result.IsOk.Should().BeTrue();
        result.Reason.Should().Be("ok");
    }

    [Fact]
    public void HallucinationGuard_rejects_uncited_roast()
    {
        var events = new[] { NewLostDeal(1, "levene") };
        var result = RoastHallucinationGuard.Verify("Levene had a rough day, no specifics here.", events);

        result.IsOk.Should().BeFalse();
        result.Reason.Should().Be("no citations found");
    }

    [Fact]
    public void HallucinationGuard_rejects_made_up_event_ids()
    {
        var events = new[] { NewLostDeal(1, "levene") };
        var result = RoastHallucinationGuard.Verify("Levene blew [evt:999] and also [evt:1234].", events);

        result.IsOk.Should().BeFalse();
        result.Reason.Should().Be("hallucinated event ids");
        result.OffendingCitations.Should().BeEquivalentTo(new[] { "999", "1234" });
    }

    [Fact]
    public void HallucinationGuard_allows_special_none_sentinel_when_no_events_exist()
    {
        var result = RoastHallucinationGuard.Verify("Nothing to say. [evt:none]", Array.Empty<ArenaEvent>());

        result.IsOk.Should().BeTrue();
    }

    // ---- StubRoastWriter -------------------------------------------------

    [Fact]
    public async Task StubRoastWriter_picks_distinct_voice_per_roaster()
    {
        var events = new[] { NewLostDeal(1, "target") };
        var writer = new StubRoastWriter();

        var fromRoma = await writer.WriteRoastAsync("roma", "target", events);
        var fromLevene = await writer.WriteRoastAsync("levene", "target", events);
        var fromMoss = await writer.WriteRoastAsync("moss", "target", events);

        // Each picks distinct opening lines from the voice table.
        fromRoma.Should().Contain("A toast");
        fromLevene.Should().Contain("let's talk");
        fromMoss.Should().Contain("Reviewing the tape");

        // All include the citation.
        Regex.Matches(fromRoma, @"\[evt:1\]").Count.Should().BeGreaterThan(0);
        Regex.Matches(fromLevene, @"\[evt:1\]").Count.Should().BeGreaterThan(0);
        Regex.Matches(fromMoss, @"\[evt:1\]").Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StubRoastWriter_handles_no_events_with_none_sentinel()
    {
        var writer = new StubRoastWriter();
        var roast = await writer.WriteRoastAsync("roma", "ghost", Array.Empty<ArenaEvent>());

        roast.Should().Contain("[evt:none]");

        // Still passes the hallucination guard.
        var guard = RoastHallucinationGuard.Verify(roast, Array.Empty<ArenaEvent>());
        guard.IsOk.Should().BeTrue();
    }

    [Fact]
    public async Task StubRoastWriter_prefers_lost_deals_over_other_event_kinds()
    {
        var touch = NewEvent(ArenaEventKinds.TouchSent, 1, "target");
        var loss = NewLostDeal(2, "target");
        var roast = await new StubRoastWriter().WriteRoastAsync("moss", "target", new[] { touch, loss });

        // Anchor on the loss (id 2), not the touch (id 1).
        roast.Should().Contain("[evt:2]");
        roast.Should().NotContain("[evt:1]");
    }

    // ---- Section integration ---------------------------------------------

    [Fact]
    public async Task RoastSection_renders_one_paragraph_per_leaderboard_pair()
    {
        await using var ledger = await SeedThreePersonaContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.Roast);
        // 3 personas → 2 pairs → 2 roasts (#1 roasts #2, #2 roasts #3).
        Regex.Matches(section.Markdown, @"\*\*[^*]+\*\* on \*\*[^*]+:\*\*").Count.Should().Be(2);

        report.Highlights.Where(h => h.Source == ReplaySectionKind.Roast).Should().HaveCount(2);
    }

    [Fact]
    public async Task RoastSection_skips_solo_contests_with_helpful_copy()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        await ledger.AppendAsync(NewClose("roma", 100_000m, T0.AddHours(1)));

        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.Roast);
        section.Markdown.Should().Contain("Need at least 2 personas");
        report.Highlights.Where(h => h.Source == ReplaySectionKind.Roast).Should().BeEmpty();
    }

    [Fact]
    public async Task RoastSection_withholds_paragraph_when_writer_produces_hallucinated_citation()
    {
        await using var ledger = await SeedThreePersonaContestAsync();

        // Bad writer that always cites event 99999 — not a real id.
        var generator = new ReplayGenerator(
            ledger,
            new LeaderboardEngine(ledger),
            new FakeTime(T0.AddHours(24)),
            customBuilders: new ISectionBuilder[]
            {
                new RoastSectionBuilder(new BadCitationWriter()),
            });

        var report = await generator.GenerateAsync(new ReplayOptions(
            Contest, new RevenueScoring(), T0.AddHours(24),
            IncludeSections: new[] { ReplaySectionKind.Roast }));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.Roast);
        section.Markdown.Should().Contain("Roast from");
        section.Markdown.Should().Contain("withheld");
        // No actual roast paragraphs survived.
        Regex.Matches(section.Markdown, @"\*\*[^*]+\*\* on \*\*[^*]+:\*\*").Count.Should().Be(0);
        report.Highlights.Should().BeEmpty();
    }

    [Fact]
    public async Task RoastSection_can_be_excluded_via_IncludeSections()
    {
        await using var ledger = await SeedThreePersonaContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(
            Contest, new RevenueScoring(), T0.AddHours(24),
            IncludeSections: ReplayOptions.DefaultSections.Where(k => k != ReplaySectionKind.Roast).ToList()));

        report.Sections.Should().NotContain(s => s.Kind == ReplaySectionKind.Roast);
        report.Markdown.Should().NotContain("🔥 The Roast");
    }

    // ---- helpers ---------------------------------------------------------

    private static ReplayGenerator NewGenerator(IArenaLedger ledger) =>
        new(ledger, new LeaderboardEngine(ledger), new FakeTime(T0.AddHours(24)));

    private static async Task<SqliteArenaLedger> SeedThreePersonaContestAsync()
    {
        var ledger = new SqliteArenaLedger("Data Source=:memory:");
        // Roma wins, Levene 2nd, Moss 3rd. Levene + Moss each have a lost deal to anchor their roasts.
        await ledger.AppendAsync(NewClose("roma", 100_000m, T0.AddHours(1)));
        await ledger.AppendAsync(NewLost("levene", 0m, T0.AddHours(2)));
        await ledger.AppendAsync(NewClose("levene", 30_000m, T0.AddHours(3)));
        await ledger.AppendAsync(NewLost("moss", 0m, T0.AddHours(4)));
        return ledger;
    }

    private static ArenaEvent NewClose(string persona, decimal value, DateTimeOffset at) =>
        new()
        {
            ContestId = Contest,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = at,
            LeadId = $"L-{persona}-W",
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload($"L-{persona}-W", persona, "Won", value, null)),
        };

    private static ArenaEvent NewLost(string persona, decimal value, DateTimeOffset at) =>
        new()
        {
            ContestId = Contest,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = at,
            LeadId = $"L-{persona}-L",
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload($"L-{persona}-L", persona, "Lost", value, "price")),
        };

    private static ArenaEvent NewLostDeal(long id, string persona) => new()
    {
        Id = id,
        ContestId = Contest,
        Kind = ArenaEventKinds.DealClosed,
        OccurredAtUtc = T0,
        LeadId = $"L-{id}",
        Persona = persona,
        PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload($"L-{id}", persona, "Lost", 0m, "price")),
    };

    private static ArenaEvent NewEvent(string kind, long id, string persona) => new()
    {
        Id = id,
        ContestId = Contest,
        Kind = kind,
        OccurredAtUtc = T0,
        LeadId = $"L-{id}",
        Persona = persona,
        PayloadJson = "{}",
    };

    private sealed class BadCitationWriter : IRoastWriter
    {
        public Task<string> WriteRoastAsync(string roaster, string target, IReadOnlyList<ArenaEvent> targetEvents, CancellationToken cancellationToken = default) =>
            Task.FromResult($"{target} blew it. [evt:99999]");
    }

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
