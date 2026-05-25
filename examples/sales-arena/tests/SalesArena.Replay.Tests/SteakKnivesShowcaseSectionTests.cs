using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay;
using Xunit;

namespace SalesArena.Replay.Tests;

/// <summary>
/// Pins the Steak Knives Showcase section (SA-08-15): celebrates #2, cites
/// the closest moment they came to taking Cadillac via LeaderboardSnapshot
/// events, no-section-when-only-one-persona, idempotent on empty snapshot
/// history.
/// </summary>
public sealed class SteakKnivesShowcaseSectionTests
{
    private const string Contest = "Tuesday-Steak-Knives";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SteakKnivesShowcaseSection_celebrates_runnerUp_with_revenue_gap_and_closest_moment()
    {
        await using var ledger = await SeedTwoPersonaContestAsync();
        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.SteakKnivesShowcase);
        section.Markdown.Should().Contain("**levene**");          // runner-up
        section.Markdown.Should().Contain("Steak Knives");
        section.Markdown.Should().Contain("Final gap to Cadillac"); // gap mention
        // Levene had a snapshot at $30K to roma's $50K → smallest gap was $20K.
        section.Markdown.Should().Contain("$20,000");

        report.Highlights.Should().Contain(h => h.Source == ReplaySectionKind.SteakKnivesShowcase
                                                 && h.Persona == "levene");
    }

    [Fact]
    public async Task SteakKnivesShowcaseSection_skips_when_only_one_persona()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        await ledger.AppendAsync(NewClose("roma", "L-001", 100_000m, T0.AddHours(1)));

        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.SteakKnivesShowcase);
        section.Markdown.Should().Contain("Need at least 2 personas");
        report.Highlights.Where(h => h.Source == ReplaySectionKind.SteakKnivesShowcase).Should().BeEmpty();
    }

    [Fact]
    public async Task SteakKnivesShowcaseSection_handles_no_snapshot_history_gracefully()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        // Just two closes — no LeaderboardSnapshot events to find a "closest moment" from.
        await ledger.AppendAsync(NewClose("roma", "L-001", 100_000m, T0.AddHours(1)));
        await ledger.AppendAsync(NewClose("levene", "L-002", 50_000m, T0.AddHours(2)));

        var generator = NewGenerator(ledger);
        var report = await generator.GenerateAsync(new ReplayOptions(Contest, new RevenueScoring(), T0.AddHours(24)));

        var section = report.Sections.Single(s => s.Kind == ReplaySectionKind.SteakKnivesShowcase);
        section.Markdown.Should().Contain("**levene**");
        section.Markdown.Should().Contain("held second the whole way"); // fallback copy
        // Highlight still surfaces.
        report.Highlights.Should().Contain(h => h.Source == ReplaySectionKind.SteakKnivesShowcase);
    }

    // ---- helpers --------------------------------------------------------

    private static ReplayGenerator NewGenerator(IArenaLedger ledger) =>
        new(ledger, new LeaderboardEngine(ledger), new FakeTimeProvider(T0.AddHours(24)));

    private static async Task<SqliteArenaLedger> SeedTwoPersonaContestAsync()
    {
        var ledger = new SqliteArenaLedger("Data Source=:memory:");
        // Roma closes a $100K total ($50K + $50K); Levene closes $30K.
        await ledger.AppendAsync(NewClose("roma", "L-001", 50_000m, T0.AddHours(1)));
        await ledger.AppendAsync(NewClose("levene", "L-101", 30_000m, T0.AddHours(2)));
        // Snapshot at hour 3: roma $50K, levene $30K — gap $20K.
        await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = Contest,
            Kind = ArenaEventKinds.LeaderboardSnapshot,
            OccurredAtUtc = T0.AddHours(3),
            PayloadJson = ArenaEvent.SerializePayload(new LeaderboardSnapshotPayload(new[]
            {
                new LeaderboardEntry("roma", 1, "Cadillac", 50_000m, 1, 0, 1.0),
                new LeaderboardEntry("levene", 2, "SteakKnives", 30_000m, 1, 0, 1.0),
            }, ScoringConfigIds.ByRevenue)),
        });
        await ledger.AppendAsync(NewClose("roma", "L-002", 50_000m, T0.AddHours(4)));
        // Snapshot at hour 5: roma $100K, levene $30K — gap $70K.
        await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = Contest,
            Kind = ArenaEventKinds.LeaderboardSnapshot,
            OccurredAtUtc = T0.AddHours(5),
            PayloadJson = ArenaEvent.SerializePayload(new LeaderboardSnapshotPayload(new[]
            {
                new LeaderboardEntry("roma", 1, "Cadillac", 100_000m, 2, 0, 1.0),
                new LeaderboardEntry("levene", 2, "SteakKnives", 30_000m, 1, 0, 1.0),
            }, ScoringConfigIds.ByRevenue)),
        });
        return ledger;
    }

    private static ArenaEvent NewClose(string persona, string leadId, decimal value, DateTimeOffset at) =>
        new()
        {
            ContestId = Contest,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = at,
            LeadId = leadId,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, "Won", value, null)),
        };

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
