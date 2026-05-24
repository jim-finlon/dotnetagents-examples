using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Replay.TrophyRoom;
using Xunit;

namespace SalesArena.Replay.Tests;

/// <summary>
/// Pins the Trophy Room contract: cross-contest aggregation, baseball-card
/// stat correctness, ranking by Cadillac wins → revenue, empty-history
/// graceful, MaxTrophies filter, OnlyPersonas filter.
/// </summary>
public sealed class TrophyRoomBuilderTests
{
    private static readonly DateTimeOffset T0 = new(2026, 4, 1, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BuildAsync_groups_by_contest_and_identifies_each_winner_from_snapshots()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        await SeedContestAsync(ledger, "Q1-Bake-Off", T0,
            winner: ("roma", 100_000m, 2),
            other: ("levene", 50_000m, 1));
        await SeedContestAsync(ledger, "Q2-Bake-Off", T0.AddDays(30),
            winner: ("levene", 80_000m, 3),
            other: ("roma", 40_000m, 1));

        var builder = new TrophyRoomBuilder(ledger, new FakeTime(T0.AddDays(60)));
        var report = await builder.BuildAsync();

        report.TotalContests.Should().Be(2);
        report.Trophies.Should().HaveCount(2);
        report.Trophies.Should().Contain(t => t.ContestId == "Q1-Bake-Off" && t.WinnerPersona == "roma" && t.WinnerRevenueUsd == 100_000m);
        report.Trophies.Should().Contain(t => t.ContestId == "Q2-Bake-Off" && t.WinnerPersona == "levene" && t.WinnerRevenueUsd == 80_000m);
        // Trophy ordering: most-recent first.
        report.Trophies[0].ContestId.Should().Be("Q2-Bake-Off");
    }

    [Fact]
    public async Task BuildAsync_aggregates_baseball_cards_across_contests()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        await SeedContestAsync(ledger, "C1", T0, winner: ("roma", 100_000m, 2), other: ("levene", 30_000m, 1));
        await SeedContestAsync(ledger, "C2", T0.AddDays(10), winner: ("roma", 200_000m, 3), other: ("levene", 100_000m, 2));
        await SeedContestAsync(ledger, "C3", T0.AddDays(20), winner: ("levene", 50_000m, 2), other: ("roma", 20_000m, 1));

        var builder = new TrophyRoomBuilder(ledger, new FakeTime(T0.AddDays(30)));
        var report = await builder.BuildAsync();

        var roma = report.BaseballCards.Single(c => c.Persona == "roma");
        roma.ContestsEntered.Should().Be(3);
        roma.CadillacWins.Should().Be(2);
        roma.LifetimeRevenueUsd.Should().Be(320_000m);  // 100 + 200 + 20
        roma.LifetimeDealsWon.Should().Be(6);            // 2 + 3 + 1
        roma.BestContestId.Should().Be("C2");
        roma.BestContestRevenueUsd.Should().Be(200_000m);

        var levene = report.BaseballCards.Single(c => c.Persona == "levene");
        levene.ContestsEntered.Should().Be(3);
        levene.CadillacWins.Should().Be(1);
        levene.LifetimeRevenueUsd.Should().Be(180_000m); // 30 + 100 + 50
        levene.LifetimeDealsWon.Should().Be(5);

        // Ordering: most Cadillac wins first.
        report.BaseballCards.Select(c => c.Persona).Should().Equal(new[] { "roma", "levene" });
    }

    [Fact]
    public async Task BuildAsync_records_signature_close_as_biggest_won_deal()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        await SeedContestAsync(ledger, "C1", T0, winner: ("roma", 50_000m, 1), other: ("levene", 20_000m, 1));
        // Roma has a $500K signature close in C2.
        await ledger.AppendAsync(NewClose("C2", "roma", "L-WHALE", 500_000m, T0.AddDays(10).AddHours(1)));
        // Snapshot makes it the C2 winner.
        await ledger.AppendAsync(NewSnapshot("C2", T0.AddDays(10).AddHours(5),
            new[] { ("roma", 1, "Cadillac", 500_000m, 1), ("levene", 2, "YouAreFired", 0m, 0) }));

        var builder = new TrophyRoomBuilder(ledger, new FakeTime(T0.AddDays(30)));
        var report = await builder.BuildAsync();

        var roma = report.BaseballCards.Single(c => c.Persona == "roma");
        roma.SignatureCloseUsd.Should().Be(500_000m);
        roma.SignatureCloseLeadId.Should().Be("L-WHALE");
    }

    [Fact]
    public async Task BuildAsync_with_empty_ledger_emits_graceful_empty_report()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var builder = new TrophyRoomBuilder(ledger, new FakeTime(T0));
        var report = await builder.BuildAsync();

        report.TotalContests.Should().Be(0);
        report.Trophies.Should().BeEmpty();
        report.BaseballCards.Should().BeEmpty();
        report.Markdown.Should().Contain("# 🏆 Trophy Room");
        report.Markdown.Should().Contain("*No contests have been recorded yet.*");
    }

    [Fact]
    public async Task BuildAsync_MaxTrophies_filter_caps_results()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        for (var i = 0; i < 5; i++)
        {
            await SeedContestAsync(ledger, $"C{i}", T0.AddDays(i),
                winner: ("roma", 100_000m, 1), other: ("levene", 50_000m, 1));
        }

        var builder = new TrophyRoomBuilder(ledger, new FakeTime(T0.AddDays(30)));
        var report = await builder.BuildAsync(new TrophyRoomOptions(MaxTrophies: 2));

        report.TotalContests.Should().Be(5);
        report.Trophies.Should().HaveCount(2);
        // Most recent two contests.
        report.Trophies.Select(t => t.ContestId).Should().Equal(new[] { "C4", "C3" });
    }

    [Fact]
    public async Task BuildAsync_OnlyPersonas_filter_limits_baseball_cards()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        await SeedContestAsync(ledger, "C1", T0, winner: ("roma", 100_000m, 1), other: ("levene", 50_000m, 1));

        var builder = new TrophyRoomBuilder(ledger, new FakeTime(T0.AddDays(30)));
        var report = await builder.BuildAsync(new TrophyRoomOptions(OnlyPersonas: new[] { "roma" }));

        report.BaseballCards.Should().HaveCount(1);
        report.BaseballCards[0].Persona.Should().Be("roma");
    }

    [Fact]
    public async Task BuildAsync_falls_back_to_revenue_when_no_snapshot_recorded()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        // Two close events, no snapshot — fallback path.
        await ledger.AppendAsync(NewClose("C1", "roma", "L-001", 75_000m, T0.AddHours(1)));
        await ledger.AppendAsync(NewClose("C1", "levene", "L-002", 100_000m, T0.AddHours(2)));

        var builder = new TrophyRoomBuilder(ledger, new FakeTime(T0.AddDays(1)));
        var report = await builder.BuildAsync();

        report.Trophies.Should().HaveCount(1);
        report.Trophies[0].WinnerPersona.Should().Be("levene");
        report.Trophies[0].WinnerRevenueUsd.Should().Be(100_000m);
    }

    // ---- helpers ---------------------------------------------------------

    private static async Task SeedContestAsync(
        SqliteArenaLedger ledger,
        string contestId,
        DateTimeOffset startAt,
        (string Persona, decimal Revenue, int Deals) winner,
        (string Persona, decimal Revenue, int Deals) other)
    {
        // Append per-deal closes for both personas (synthesize unit deals).
        var winnerDealValue = winner.Revenue / Math.Max(1, winner.Deals);
        for (var i = 0; i < winner.Deals; i++)
        {
            await ledger.AppendAsync(NewClose(contestId, winner.Persona, $"L-W{i}", winnerDealValue, startAt.AddHours(1 + i)));
        }
        var otherDealValue = other.Revenue / Math.Max(1, other.Deals);
        for (var i = 0; i < other.Deals; i++)
        {
            await ledger.AppendAsync(NewClose(contestId, other.Persona, $"L-O{i}", otherDealValue, startAt.AddHours(2 + i)));
        }
        // Snapshot that names the winner.
        await ledger.AppendAsync(NewSnapshot(contestId, startAt.AddHours(10), new[]
        {
            (winner.Persona, 1, "Cadillac", winner.Revenue, winner.Deals),
            (other.Persona, 2, "YouAreFired", other.Revenue, other.Deals),
        }));
    }

    private static ArenaEvent NewClose(string contestId, string persona, string leadId, decimal value, DateTimeOffset at) =>
        new()
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = at,
            LeadId = leadId,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, "Won", value, null)),
        };

    private static ArenaEvent NewSnapshot(string contestId, DateTimeOffset at, (string Persona, int Position, string Tier, decimal Revenue, int Wins)[] entries) =>
        new()
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.LeaderboardSnapshot,
            OccurredAtUtc = at,
            PayloadJson = ArenaEvent.SerializePayload(new LeaderboardSnapshotPayload(
                entries.Select(e => new LeaderboardEntry(e.Persona, e.Position, e.Tier, e.Revenue, e.Wins, 0, 1.0)).ToList(),
                "ByRevenue")),
        };

    private sealed class FakeTime : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTime(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
