using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay;
using Xunit;

namespace SalesArena.Replay.Tests;

/// <summary>
/// End-to-end tests for the replay engine — feeds synthetic ledger events
/// through SqliteArenaLedger + LeaderboardEngine + ReplayGenerator and
/// asserts each section renders correctly + the highlights surface the
/// right anchor events.
/// </summary>
public sealed class ReplayGeneratorTests
{
    private const string Contest = "Tuesday-Steak-Knives";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GenerateAsync_emits_all_5_default_sections_with_assembled_markdown()
    {
        await using var ledger = await SeedRichContestAsync();
        var engine = new LeaderboardEngine(ledger);
        var generator = new ReplayGenerator(ledger, engine, FixedTime(T0.AddHours(24)));

        var report = await generator.GenerateAsync(new ReplayOptions(
            ContestId: Contest,
            FinalScoring: new RevenueScoring(),
            AsOfUtc: T0.AddHours(24),
            ContestDisplayName: "Tuesday's Steak-Knives Bake-Off"));

        report.Sections.Should().HaveCount(7);
        report.Sections.Select(s => s.Kind).Should().Equal(ReplayOptions.DefaultSections);

        // Markdown contains the top-level title + each section header (emoji-bearing).
        report.Markdown.Should().Contain("# Sales Arena Replay — Tuesday's Steak-Knives Bake-Off");
        report.Markdown.Should().Contain("🏆 Final Leaderboard");
        report.Markdown.Should().Contain("📜 Persona Deal Logs");
        report.Markdown.Should().Contain("🔪 Steak Knives Showcase");
        report.Markdown.Should().Contain("🔪 Closest Call");
        report.Markdown.Should().Contain("🚀 Best Comeback");
        report.Markdown.Should().Contain("🔔 MVP Touch");
        report.Markdown.Should().Contain("🔥 The Roast");

        // Word-count threshold: a non-trivial replay should produce > 100 words.
        report.Markdown.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task LeaderboardSection_shows_tier_glyphs_and_top_persona_highlight()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var leaderboardSection = report.Sections.Single(s => s.Kind == ReplaySectionKind.Leaderboard);
        leaderboardSection.Markdown.Should().Contain("🚗 Cadillac");
        leaderboardSection.Markdown.Should().Contain("🔪 Steak Knives");
        leaderboardSection.Markdown.Should().Contain("📦 You're Fired");

        report.Highlights.Should().Contain(h => h.Source == ReplaySectionKind.Leaderboard
                                                 && h.Headline.Contains("Cadillac", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PersonaDealLogSection_lists_every_persona_with_at_least_one_close()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.PersonaDealLog);
        section.Markdown.Should().Contain("### roma");
        section.Markdown.Should().Contain("### levene");
        section.Markdown.Should().Contain("### moss");
        section.Markdown.Should().Contain("✅");        // at least one win glyph
        section.Markdown.Should().Contain("❌");        // at least one loss glyph
    }

    [Fact]
    public async Task ClosestCallSection_picks_the_largest_lost_deal()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.ClosestCall);
        // The seed deliberately included a $75K lost deal (L-401, levene, lost after 3 touches + a proposal).
        section.Markdown.Should().Contain("**L-401**");
        section.Markdown.Should().Contain("**levene**");
        section.Markdown.Should().Contain("$75,000");

        report.Highlights.Should().Contain(h => h.Source == ReplaySectionKind.ClosestCall
                                                 && h.LeadId == "L-401"
                                                 && h.Persona == "levene");
    }

    [Fact]
    public async Task BestComebackSection_picks_persona_with_biggest_position_climb()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.BestComeback);
        // The seed includes a leaderboard snapshot where aaronow is in #3, then aaronow lands at #1 in the final → 2 position climb.
        section.Markdown.Should().Contain("**aaronow**");
        section.Markdown.Should().Contain("climbed");
    }

    [Fact]
    public async Task MvpTouchSection_pairs_winning_deal_with_its_last_touch()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.MvpTouch);
        // Biggest win: aaronow's $200K on L-501. MVP attribution = the LAST touch before the close
        // (the conventional "closer touch"), which in the seed is the 'follow-up' template at T0+5h.
        section.Markdown.Should().Contain("L-501");
        section.Markdown.Should().Contain("$200,000");
        section.Markdown.Should().Contain("follow-up");
        section.Markdown.Should().Contain("Touching base");

        report.Highlights.Should().Contain(h => h.Source == ReplaySectionKind.MvpTouch
                                                 && h.LeadId == "L-501"
                                                 && h.ValueUsd == 200_000m);
    }

    [Fact]
    public async Task GenerateAsync_with_empty_ledger_emits_empty_but_well_formed_report()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0));

        report.Sections.Should().HaveCount(7);
        report.Sections.Should().AllSatisfy(s => s.Markdown.Should().NotBeNullOrWhiteSpace());
        report.Highlights.Should().BeEmpty();
        // Even with no events, the Markdown should have a title + the 7 section headers.
        report.Markdown.Should().Contain("🏆 Final Leaderboard");
        report.Markdown.Should().Contain("🔪 Steak Knives Showcase");
        report.Markdown.Should().Contain("🔥 The Roast");
    }

    [Fact]
    public async Task IncludeSections_filter_renders_only_requested_kinds()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(
            ContestId: Contest,
            FinalScoring: new RevenueScoring(),
            AsOfUtc: T0.AddHours(24),
            IncludeSections: new[] { ReplaySectionKind.Leaderboard, ReplaySectionKind.MvpTouch }));

        report.Sections.Should().HaveCount(2);
        report.Sections.Select(s => s.Kind).Should().Equal(new[] { ReplaySectionKind.Leaderboard, ReplaySectionKind.MvpTouch });
        report.Markdown.Should().NotContain("📜 Persona Deal Logs");
    }

    [Fact]
    public async Task ExportToFileAsync_writes_the_assembled_markdown_to_disk()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);

        var path = Path.Combine(Path.GetTempPath(), $"replay-{Guid.NewGuid():N}.md");
        try
        {
            var report = await generator.ExportToFileAsync(
                new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)),
                path);

            File.Exists(path).Should().BeTrue();
            var fromDisk = await File.ReadAllTextAsync(path);
            fromDisk.Should().Be(report.Markdown);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Custom_template_dir_overrides_default_headers()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);

        var tempDir = Path.Combine(Path.GetTempPath(), $"replay-tmpl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(tempDir, "leaderboard.md"),
                "## CUSTOM_HEADER_FOR_{{contest_id}} — generated {{generated_at_utc}}");

            var report = await generator.GenerateAsync(new ReplayOptions(
                ContestId: Contest,
                FinalScoring: new RevenueScoring(),
                AsOfUtc: T0.AddHours(24),
                TemplateDir: tempDir));

            report.Markdown.Should().Contain($"CUSTOM_HEADER_FOR_{Contest}");
            report.Markdown.Should().Contain("generated 2026-05-19");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task Highlights_aggregate_from_all_sections()
    {
        await using var ledger = await SeedRichContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        // At least: Leaderboard's Cadillac + ClosestCall + (BestComeback if any climb) + MvpTouch.
        var sources = report.Highlights.Select(h => h.Source).Distinct().ToList();
        sources.Should().Contain(ReplaySectionKind.Leaderboard);
        sources.Should().Contain(ReplaySectionKind.ClosestCall);
        sources.Should().Contain(ReplaySectionKind.MvpTouch);
    }

    // ---- seed ------------------------------------------------------------

    private static ReplayGenerator NewGenerator(IArenaLedger ledger) =>
        new(ledger, new LeaderboardEngine(ledger), FixedTime(T0.AddHours(24)));

    /// <summary>
    /// Build a rich synthetic contest with 5 personas, 6+ deals (mix of won/lost),
    /// touches, a proposal-attached lost deal, and 2 leaderboard snapshots so
    /// the BestComeback section has something to find.
    /// </summary>
    private static async Task<SqliteArenaLedger> SeedRichContestAsync()
    {
        var ledger = new SqliteArenaLedger("Data Source=:memory:");

        // --- Roma: $100K won (L-101) + $0 loss (L-102) ---
        await Append(ledger, ArenaEventKinds.LeadAssigned, T0.AddHours(0.5), "L-101", "roma", new LeadAssignedPayload("L-101", "roma", "round-robin"));
        await Append(ledger, ArenaEventKinds.TouchSent, T0.AddHours(1), "L-101", "roma",
            new TouchSentPayload("L-101", "roma", "email", "tmpl-formal", "v1", "Open dialog with Roma", 220));
        await Append(ledger, ArenaEventKinds.DealClosed, T0.AddHours(3), "L-101", "roma",
            new DealClosedPayload("L-101", "roma", "Won", 100_000m, null));
        await Append(ledger, ArenaEventKinds.DealClosed, T0.AddHours(4), "L-102", "roma",
            new DealClosedPayload("L-102", "roma", "Lost", 0m, "no-budget"));

        // --- Aaronow: $200K + $40K won; came from behind ---
        await Append(ledger, ArenaEventKinds.LeadAssigned, T0.AddHours(0.5), "L-501", "aaronow", new LeadAssignedPayload("L-501", "aaronow", "round-robin"));
        await Append(ledger, ArenaEventKinds.TouchSent, T0.AddHours(2), "L-501", "aaronow",
            new TouchSentPayload("L-501", "aaronow", "email", "consultative-email", "v2", "Hello from George", 360));
        await Append(ledger, ArenaEventKinds.TouchSent, T0.AddHours(5), "L-501", "aaronow",
            new TouchSentPayload("L-501", "aaronow", "email", "follow-up", "v1", "Touching base", 180));
        await Append(ledger, ArenaEventKinds.DealClosed, T0.AddHours(8), "L-501", "aaronow",
            new DealClosedPayload("L-501", "aaronow", "Won", 200_000m, null));
        await Append(ledger, ArenaEventKinds.DealClosed, T0.AddHours(9), "L-502", "aaronow",
            new DealClosedPayload("L-502", "aaronow", "Won", 40_000m, null));

        // --- Levene: $30K won (L-301) + a lost $75K with a proposal trail (L-401) — the closest call ---
        await Append(ledger, ArenaEventKinds.DealClosed, T0.AddHours(2.5), "L-301", "levene",
            new DealClosedPayload("L-301", "levene", "Won", 30_000m, null));
        await Append(ledger, ArenaEventKinds.LeadAssigned, T0.AddHours(0.5), "L-401", "levene", new LeadAssignedPayload("L-401", "levene", "round-robin"));
        await Append(ledger, ArenaEventKinds.TouchSent, T0.AddHours(1.5), "L-401", "levene",
            new TouchSentPayload("L-401", "levene", "sms", "tmpl-urgent", "v3", "Quick check", 60));
        await Append(ledger, ArenaEventKinds.TouchSent, T0.AddHours(3.5), "L-401", "levene",
            new TouchSentPayload("L-401", "levene", "sms", "tmpl-urgent", "v3", "Following up", 60));
        await Append(ledger, ArenaEventKinds.TouchSent, T0.AddHours(5.5), "L-401", "levene",
            new TouchSentPayload("L-401", "levene", "sms", "tmpl-urgent", "v3", "Last chance", 60));
        await Append(ledger, ArenaEventKinds.ProposalSent, T0.AddHours(6), "L-401", "levene",
            new ProposalSentPayload("L-401", "levene", "pro", 75_000m, "prop-99"));
        await Append(ledger, ArenaEventKinds.DealClosed, T0.AddHours(10), "L-401", "levene",
            new DealClosedPayload("L-401", "levene", "Lost", 0m, "price-sensitivity"));

        // --- Moss: 1 small loss ---
        await Append(ledger, ArenaEventKinds.DealClosed, T0.AddHours(7), "L-601", "moss",
            new DealClosedPayload("L-601", "moss", "Lost", 0m, "wrong-fit"));

        // --- Williamson: just a touch, no closes ---
        await Append(ledger, ArenaEventKinds.TouchSent, T0.AddHours(1), "L-701", "williamson",
            new TouchSentPayload("L-701", "williamson", "linkedin", "tmpl-dm", "v1", null, 90));

        // --- Leaderboard snapshots so BestComeback has data ---
        // Snapshot at hour 5: roma #1, levene #2, aaronow #3 (climber starts here)
        await Append(ledger, ArenaEventKinds.LeaderboardSnapshot, T0.AddHours(5), null, null,
            new LeaderboardSnapshotPayload(new[]
            {
                new LeaderboardEntry("roma", 1, "Cadillac", 100_000m, 1, 0, 1.0),
                new LeaderboardEntry("levene", 2, "SteakKnives", 30_000m, 1, 0, 1.0),
                new LeaderboardEntry("aaronow", 3, "SteakKnives", 0m, 0, 0, 0.0),
            }, ScoringConfigIds.ByRevenue));
        // Snapshot at hour 12: now aaronow leads after the $200K close
        await Append(ledger, ArenaEventKinds.LeaderboardSnapshot, T0.AddHours(12), null, null,
            new LeaderboardSnapshotPayload(new[]
            {
                new LeaderboardEntry("aaronow", 1, "Cadillac", 240_000m, 2, 0, 1.0),
                new LeaderboardEntry("roma", 2, "SteakKnives", 100_000m, 1, 1, 0.5),
                new LeaderboardEntry("levene", 3, "YouAreFired", 30_000m, 1, 1, 0.5),
            }, ScoringConfigIds.ByRevenue));

        return ledger;
    }

    private static Task Append<T>(IArenaLedger ledger, string kind, DateTimeOffset at, string? leadId, string? persona, T payload) where T : class
    {
        return ledger.AppendAsync(new ArenaEvent
        {
            ContestId = Contest,
            Kind = kind,
            OccurredAtUtc = at,
            LeadId = leadId,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        });
    }

    private static TimeProvider FixedTime(DateTimeOffset at) => new FakeTimeProvider(at);

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
