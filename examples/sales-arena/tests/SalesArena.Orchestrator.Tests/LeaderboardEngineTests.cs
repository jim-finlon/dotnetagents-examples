using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using Xunit;

namespace SalesArena.Orchestrator.Tests;

/// <summary>
/// Pins the leaderboard engine + 4 scoring configs + tier classification +
/// tier-change event emission + snapshot persistence.
/// </summary>
public sealed class LeaderboardEngineTests
{
    private const string Contest = "Tuesday-Steak-Knives";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset AsOf = T0.AddHours(24);

    [Fact]
    public async Task ComputeAsync_returns_empty_leaderboard_when_no_events()
    {
        await using var ledger = NewLedger();
        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new RevenueScoring(), AsOf);

        board.Entries.Should().BeEmpty();
        board.ContestId.Should().Be(Contest);
        board.ScoringConfigId.Should().Be(ScoringConfigIds.ByRevenue);
        board.AsOfUtc.Should().Be(AsOf);
    }

    [Fact]
    public async Task ComputeAsync_ranks_personas_by_revenue_in_a_5_persona_contest()
    {
        await using var ledger = NewLedger();
        // Roma: $100K (1 deal won, 1 lost)
        await AppendClose(ledger, "roma", "L-001", 100_000m, "Won");
        await AppendClose(ledger, "roma", "L-002", 0m, "Lost");
        // Levene: $30K (3 deals won, 4 lost)
        await AppendClose(ledger, "levene", "L-101", 10_000m, "Won");
        await AppendClose(ledger, "levene", "L-102", 12_000m, "Won");
        await AppendClose(ledger, "levene", "L-103", 8_000m, "Won");
        for (var i = 104; i < 108; i++) await AppendClose(ledger, "levene", $"L-{i}", 0m, "Lost");
        // Moss: $80K (1 won, 0 lost)
        await AppendClose(ledger, "moss", "L-201", 80_000m, "Won");
        // Aaronow: $40K (2 won, 1 lost)
        await AppendClose(ledger, "aaronow", "L-301", 25_000m, "Won");
        await AppendClose(ledger, "aaronow", "L-302", 15_000m, "Won");
        await AppendClose(ledger, "aaronow", "L-303", 0m, "Lost");
        // Williamson: $0 (no closes — but appears via 5 touches sent)
        for (var i = 0; i < 5; i++) await AppendKind(ledger, "williamson", ArenaEventKinds.TouchSent, $"L-{400 + i}");

        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new RevenueScoring(), AsOf);

        board.Entries.Should().HaveCount(5);
        board.Entries.Select(e => e.Persona).Should().Equal(new[] { "roma", "moss", "aaronow", "levene", "williamson" });
        board.Entries.Select(e => e.RevenueUsd).Should().Equal(new[] { 100_000m, 80_000m, 40_000m, 30_000m, 0m });

        // Tier assignment: 5 personas → position 1 Cadillac, 2-3 SteakKnives, 4-5 YouAreFired
        // (ceil(5/2) = 3 → SteakKnives positions are 2 and 3)
        board.Entries[0].Tier.Should().Be(LeaderboardTier.Cadillac);
        board.Entries[1].Tier.Should().Be(LeaderboardTier.SteakKnives);
        board.Entries[2].Tier.Should().Be(LeaderboardTier.SteakKnives);
        board.Entries[3].Tier.Should().Be(LeaderboardTier.YouAreFired);
        board.Entries[4].Tier.Should().Be(LeaderboardTier.YouAreFired);
    }

    [Fact]
    public async Task DealCountScoring_ranks_by_pure_win_count()
    {
        await using var ledger = NewLedger();
        // Roma: 1 huge deal
        await AppendClose(ledger, "roma", "L-001", 500_000m, "Won");
        // Levene: 4 small deals
        for (var i = 0; i < 4; i++) await AppendClose(ledger, "levene", $"L-1{i:00}", 10_000m, "Won");

        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new DealCountScoring(), AsOf);

        // Levene's 4 deals win the count race, even though Roma has 12x the revenue.
        board.Entries[0].Persona.Should().Be("levene");
        board.Entries[0].DealsWon.Should().Be(4);
        board.Entries[1].Persona.Should().Be("roma");
        board.Entries[1].DealsWon.Should().Be(1);
    }

    [Fact]
    public async Task ConversionScoring_requires_minimum_decisions_for_ranking()
    {
        await using var ledger = NewLedger();
        // Lucky levene: 1 win, 0 losses → would be 100% conversion
        await AppendClose(ledger, "levene", "L-101", 10_000m, "Won");
        // Hardworking roma: 4 wins, 4 losses → 50% conversion
        for (var i = 0; i < 4; i++) await AppendClose(ledger, "roma", $"L-2{i:00}", 20_000m, "Won");
        for (var i = 0; i < 4; i++) await AppendClose(ledger, "roma", $"L-3{i:00}", 0m, "Lost");

        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new ConversionScoring(minDecisionsForRanking: 3), AsOf);

        // Levene falls below the minimum-decisions floor and scores 0.
        // Roma's 50% win rate beats Levene.
        board.Entries[0].Persona.Should().Be("roma");
        board.Entries[0].WinRate.Should().Be(0.5);
        board.Entries[1].Persona.Should().Be("levene");
        // Levene's row still shows their raw stats — but their score is 0 under this config.
        board.Entries[1].Score.Should().Be(0.0);
    }

    [Fact]
    public async Task CompositeScoring_blends_revenue_winrate_and_speed()
    {
        await using var ledger = NewLedger();
        // Pre-seed lead assignments so AvgTimeToClose can be computed.
        // Roma: 2 wins, sub-day close → fast + win-rate + decent revenue
        await AppendAssign(ledger, "roma", "L-001", T0);
        await AppendAssign(ledger, "roma", "L-002", T0);
        await AppendClose(ledger, "roma", "L-001", 50_000m, "Won", T0.AddHours(8));
        await AppendClose(ledger, "roma", "L-002", 50_000m, "Won", T0.AddHours(12));
        // Levene: 1 win, 7-day close → slower + revenue close to roma's per-deal
        await AppendAssign(ledger, "levene", "L-101", T0);
        await AppendClose(ledger, "levene", "L-101", 80_000m, "Won", T0.AddDays(7));

        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new CompositeScoring(), AsOf);

        // Roma's faster close + 100% win rate edges Levene's higher per-deal revenue.
        board.Entries[0].Persona.Should().Be("roma");
        board.Entries[0].Tier.Should().Be(LeaderboardTier.Cadillac);
    }

    [Fact]
    public async Task LeaderboardChanged_fires_when_position_swaps()
    {
        await using var ledger = NewLedger();
        await AppendClose(ledger, "roma", "L-001", 50_000m, "Won");
        await AppendClose(ledger, "levene", "L-101", 30_000m, "Won");

        var engine = new LeaderboardEngine(ledger);
        LeaderboardChangedEventArgs? captured = null;
        engine.LeaderboardChanged += (_, e) => captured = e;

        var first = await engine.ComputeAsync(Contest, new RevenueScoring(), T0.AddHours(1));
        first.Entries[0].Persona.Should().Be("roma");
        // First compute under a config: no Previous → no change event.
        captured.Should().BeNull();

        // Levene closes a bigger deal; on the next compute, levene leads.
        await AppendClose(ledger, "levene", "L-102", 100_000m, "Won", T0.AddHours(2));
        var second = await engine.ComputeAsync(Contest, new RevenueScoring(), T0.AddHours(3));
        second.Entries[0].Persona.Should().Be("levene");

        captured.Should().NotBeNull();
        captured!.Changes.Should().HaveCountGreaterThanOrEqualTo(2);
        captured.Changes.Should().Contain(c => c.Persona == "levene" && c.ToPosition == 1 && c.ToTier == LeaderboardTier.Cadillac);
        captured.Changes.Should().Contain(c => c.Persona == "roma" && c.FromTier == LeaderboardTier.Cadillac && c.FromPosition == 1);
    }

    [Fact]
    public async Task LeaderboardChanged_does_not_fire_when_ranking_is_unchanged()
    {
        await using var ledger = NewLedger();
        await AppendClose(ledger, "roma", "L-001", 50_000m, "Won");
        await AppendClose(ledger, "levene", "L-101", 30_000m, "Won");

        var engine = new LeaderboardEngine(ledger);
        await engine.ComputeAsync(Contest, new RevenueScoring(), T0.AddHours(1));

        int fireCount = 0;
        engine.LeaderboardChanged += (_, _) => fireCount++;

        // Recompute with the exact same state — no change event should fire.
        await engine.ComputeAsync(Contest, new RevenueScoring(), T0.AddHours(2));
        fireCount.Should().Be(0);
    }

    [Fact]
    public async Task TieBreak_uses_revenue_then_deals_then_persona_name_ordinal()
    {
        await using var ledger = NewLedger();
        // Both roma and levene end with the same DealCount=2 — break by revenue.
        await AppendClose(ledger, "roma", "L-001", 30_000m, "Won");
        await AppendClose(ledger, "roma", "L-002", 30_000m, "Won");
        await AppendClose(ledger, "levene", "L-101", 50_000m, "Won");
        await AppendClose(ledger, "levene", "L-102", 50_000m, "Won");

        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new DealCountScoring(), AsOf);

        // Same dealsWon (2) → revenue tiebreak → levene wins
        board.Entries[0].Persona.Should().Be("levene");
        board.Entries[1].Persona.Should().Be("roma");
    }

    [Fact]
    public async Task LeaderboardSnapshotter_persists_a_LeaderboardSnapshot_event()
    {
        await using var ledger = NewLedger();
        await AppendClose(ledger, "roma", "L-001", 50_000m, "Won");
        await AppendClose(ledger, "levene", "L-101", 30_000m, "Won");

        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new RevenueScoring(), AsOf);

        var snapshotter = new LeaderboardSnapshotter(ledger);
        var saved = await snapshotter.SnapshotAsync(board);

        saved.Id.Should().BeGreaterThan(0);
        saved.Kind.Should().Be(ArenaEventKinds.LeaderboardSnapshot);

        var payload = saved.GetPayload<LeaderboardSnapshotPayload>()!;
        payload.ScoringConfigId.Should().Be(ScoringConfigIds.ByRevenue);
        payload.Entries.Should().HaveCount(2);
        payload.Entries[0].Persona.Should().Be("roma");
        payload.Entries[0].Tier.Should().Be(LeaderboardTierNames.Cadillac);
        payload.Entries[0].RevenueUsd.Should().Be(50_000m);
    }

    [Fact]
    public async Task ScoringConfigs_each_have_a_distinct_id_and_name()
    {
        var configs = new IScoringConfig[]
        {
            new RevenueScoring(),
            new DealCountScoring(),
            new ConversionScoring(),
            new CompositeScoring(),
        };
        configs.Select(c => c.Id).Should().OnlyHaveUniqueItems();
        configs.Select(c => c.Name).Should().OnlyHaveUniqueItems();
        configs.Select(c => c.Id).Should().BeEquivalentTo(new[]
        {
            ScoringConfigIds.ByRevenue,
            ScoringConfigIds.ByDealCount,
            ScoringConfigIds.ByConversion,
            ScoringConfigIds.ByComposite,
        });
    }

    [Fact]
    public void CompositeScoring_rejects_negative_weights()
    {
        var act = () => new CompositeScoring(revenueWeight: -0.1, winRateWeight: 0.6, speedWeight: 0.5);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CompositeScoring_rejects_weights_that_dont_sum_to_one()
    {
        var act = () => new CompositeScoring(revenueWeight: 0.4, winRateWeight: 0.4, speedWeight: 0.4);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Compute_filters_events_strictly_in_one_contest()
    {
        await using var ledger = NewLedger();
        // Two contests in same ledger.
        await AppendClose(ledger, "roma", "L-001", 100_000m, "Won", contestId: "ContestA");
        await AppendClose(ledger, "roma", "L-002", 5_000m, "Won", contestId: "ContestB");

        var engine = new LeaderboardEngine(ledger);
        var a = await engine.ComputeAsync("ContestA", new RevenueScoring(), AsOf);
        var b = await engine.ComputeAsync("ContestB", new RevenueScoring(), AsOf);

        a.Entries.Should().HaveCount(1);
        a.Entries[0].RevenueUsd.Should().Be(100_000m);
        b.Entries.Should().HaveCount(1);
        b.Entries[0].RevenueUsd.Should().Be(5_000m);
    }

    [Fact]
    public async Task Compute_with_asOfUtc_excludes_later_events()
    {
        await using var ledger = NewLedger();
        await AppendClose(ledger, "roma", "L-001", 50_000m, "Won", T0.AddHours(1));
        await AppendClose(ledger, "roma", "L-002", 50_000m, "Won", T0.AddHours(48)); // outside window

        var engine = new LeaderboardEngine(ledger);
        var board = await engine.ComputeAsync(Contest, new RevenueScoring(), AsOf);

        board.Entries.Should().HaveCount(1);
        board.Entries[0].RevenueUsd.Should().Be(50_000m); // only the in-window deal
    }

    // ---- helpers ---------------------------------------------------------

    private static SqliteArenaLedger NewLedger() => new("Data Source=:memory:");

    private static async Task AppendClose(IArenaLedger ledger, string persona, string leadId, decimal? value, string outcome, DateTimeOffset? at = null, string contestId = Contest)
    {
        var payload = new DealClosedPayload(LeadId: leadId, Persona: persona, Outcome: outcome, ValueUsd: value, LossReason: null);
        await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.DealClosed,
            OccurredAtUtc = at ?? T0.AddHours(1),
            LeadId = leadId,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        });
    }

    private static async Task AppendAssign(IArenaLedger ledger, string persona, string leadId, DateTimeOffset at, string contestId = Contest)
    {
        var payload = new LeadAssignedPayload(LeadId: leadId, Persona: persona, Source: "test");
        await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.LeadAssigned,
            OccurredAtUtc = at,
            LeadId = leadId,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        });
    }

    private static async Task AppendKind(IArenaLedger ledger, string persona, string kind, string leadId, DateTimeOffset? at = null, string contestId = Contest)
    {
        await ledger.AppendAsync(new ArenaEvent
        {
            ContestId = contestId,
            Kind = kind,
            OccurredAtUtc = at ?? T0.AddHours(1),
            LeadId = leadId,
            Persona = persona,
            PayloadJson = "{}",
        });
    }
}
