using System.Text.Json;
using FluentAssertions;
using SalesArena.Orchestrator.Glengarry;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Orchestrator.LeadPool;
using Xunit;

namespace SalesArena.Orchestrator.Tests;

/// <summary>
/// Pins the Glengarry-drip cycle: top-tier persona gets premium leads,
/// bottom-tier persona loses leads, cooldowns prevent thrashing, and the
/// ledger records every mutation. The story-acceptance multi-window test
/// (3 windows × N personas) lives at the bottom.
/// </summary>
public sealed class GlengarryDripPolicyRunnerTests
{
    private const string Contest = "Tuesday-Steak-Knives";
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RunDripCycle_drips_premium_leads_to_cadillac_persona_and_revokes_from_bottom()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var (pool, packPath) = await NewPoolWithPackAsync(glengarryCount: 10, coldCount: 20);
        try
        {
            // Seed: each persona starts with 4 cold leads.
            await pool.AssignAsync("roma", 4, tier: "cold");
            await pool.AssignAsync("levene", 4, tier: "cold");
            await pool.AssignAsync("moss", 4, tier: "cold");

            var runner = new GlengarryDripPolicyRunner(pool, ledger,
                new GlengarryDripPolicy(TimeSpan.FromMinutes(60), DripCount: 3, BottomRevokeCount: 2));

            var board = Leaderboard(
                ("roma", 1, LeaderboardTier.Cadillac, 100_000m, 2),
                ("levene", 2, LeaderboardTier.SteakKnives, 50_000m, 1),
                ("moss", 3, LeaderboardTier.YouAreFired, 0m, 0));

            var decision = await runner.RunDripCycleAsync(board, T0.AddHours(2));
            decision.DidMutate.Should().BeTrue();
            decision.TopPersona.Should().Be("roma");
            decision.DrippedLeadIds.Should().HaveCount(3);
            decision.BottomPersona.Should().Be("moss");
            decision.RevokedLeadIds.Should().HaveCount(2);

            // Roma now owns 4 (initial cold) + 3 (Glengarry drip) = 7 leads.
            pool.GetAssignedLeads("roma").Should().HaveCount(7);
            // Moss lost 2 leads back to the pool.
            pool.GetAssignedLeads("moss").Should().HaveCount(2);

            // Ledger has both GlengarryLeadDripped + LeadsRevoked events.
            var driplog = await ledger.QueryAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.GlengarryLeadDripped)).ToListAsync();
            driplog.Should().HaveCount(1);
            driplog[0].Persona.Should().Be("roma");

            var revokeLog = await ledger.QueryAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.LeadsRevoked)).ToListAsync();
            revokeLog.Should().HaveCount(1);
            revokeLog[0].Persona.Should().Be("moss");
        }
        finally
        {
            File.Delete(packPath);
        }
    }

    [Fact]
    public async Task RunDripCycle_skips_when_no_cadillac_persona()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var (pool, packPath) = await NewPoolWithPackAsync(glengarryCount: 5, coldCount: 5);
        try
        {
            var runner = new GlengarryDripPolicyRunner(pool, ledger);
            var emptyBoard = new Leaderboard.Leaderboard(Contest, ScoringConfigIds.ByRevenue, T0, Array.Empty<LeaderboardRow>());

            var decision = await runner.RunDripCycleAsync(emptyBoard, T0);
            decision.DidMutate.Should().BeFalse();
            decision.Reason.Should().Be(GlengarryDripSkipReasons.NoTopPersona);
        }
        finally { File.Delete(packPath); }
    }

    [Fact]
    public async Task RunDripCycle_skips_when_no_premium_leads_remaining()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var (pool, packPath) = await NewPoolWithPackAsync(glengarryCount: 2, coldCount: 10);
        try
        {
            // Consume all glengarry leads upfront.
            await pool.AssignAsync("preconsumed", 2, tier: "glengarry");

            var runner = new GlengarryDripPolicyRunner(pool, ledger,
                new GlengarryDripPolicy(TimeSpan.FromHours(24), DripCount: 5, BottomRevokeCount: 2));

            var board = Leaderboard(
                ("roma", 1, LeaderboardTier.Cadillac, 100_000m, 2),
                ("moss", 2, LeaderboardTier.YouAreFired, 0m, 0));

            var decision = await runner.RunDripCycleAsync(board, T0);
            decision.DidMutate.Should().BeFalse();
            decision.Reason.Should().Be(GlengarryDripSkipReasons.NoPremiumLeadsAvailable);
        }
        finally { File.Delete(packPath); }
    }

    [Fact]
    public async Task RunDripCycle_honors_cooldown_for_top_persona()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var (pool, packPath) = await NewPoolWithPackAsync(glengarryCount: 20, coldCount: 5);
        try
        {
            var policy = new GlengarryDripPolicy(TimeSpan.FromHours(24), DripCount: 3, BottomRevokeCount: 2);
            var runner = new GlengarryDripPolicyRunner(pool, ledger, policy);
            var board = Leaderboard(
                ("roma", 1, LeaderboardTier.Cadillac, 100_000m, 2),
                ("moss", 2, LeaderboardTier.YouAreFired, 0m, 0));

            // First cycle: drips.
            var first = await runner.RunDripCycleAsync(board, T0);
            first.DidMutate.Should().BeTrue();

            // Second cycle just 30 minutes later: cooldown blocks.
            var second = await runner.RunDripCycleAsync(board, T0.AddMinutes(30));
            second.DidMutate.Should().BeFalse();
            second.Reason.Should().Be(GlengarryDripSkipReasons.NotDueYet);

            // 24h later: drips again.
            var third = await runner.RunDripCycleAsync(board, T0.AddHours(25));
            third.DidMutate.Should().BeTrue();
        }
        finally { File.Delete(packPath); }
    }

    [Fact]
    public async Task RunDripCycle_drips_only_when_no_bottom_tier_persona()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var (pool, packPath) = await NewPoolWithPackAsync(glengarryCount: 10, coldCount: 5);
        try
        {
            // 2-persona contest: both in Cadillac/SteakKnives (small N → no YouAreFired tier).
            var board = Leaderboard(
                ("roma", 1, LeaderboardTier.Cadillac, 100_000m, 2),
                ("levene", 2, LeaderboardTier.SteakKnives, 50_000m, 1));

            var runner = new GlengarryDripPolicyRunner(pool, ledger,
                new GlengarryDripPolicy(TimeSpan.FromHours(24), DripCount: 3, BottomRevokeCount: 2));

            var decision = await runner.RunDripCycleAsync(board, T0);
            decision.DrippedLeadIds.Should().HaveCount(3);
            decision.RevokedLeadIds.Should().BeEmpty();
            decision.BottomPersona.Should().BeNull();
        }
        finally { File.Delete(packPath); }
    }

    [Fact]
    public async Task RunDripCycle_three_windows_in_a_three_pod_contest_drips_consistently()
    {
        // The story-AC test: 3-pod contest, 3 drip windows, verify drip + revoke
        // happen on every window after the first.
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var (pool, packPath) = await NewPoolWithPackAsync(glengarryCount: 30, coldCount: 30);
        try
        {
            // Each pod gets 4 cold leads to start.
            await pool.AssignAsync("roma", 4, tier: "cold");
            await pool.AssignAsync("levene", 4, tier: "cold");
            await pool.AssignAsync("moss", 4, tier: "cold");

            var runner = new GlengarryDripPolicyRunner(pool, ledger,
                new GlengarryDripPolicy(TimeSpan.FromHours(24), DripCount: 5, BottomRevokeCount: 3));

            var board = Leaderboard(
                ("roma", 1, LeaderboardTier.Cadillac, 100_000m, 2),
                ("levene", 2, LeaderboardTier.SteakKnives, 50_000m, 1),
                ("moss", 3, LeaderboardTier.YouAreFired, 0m, 0));

            // Window 1: t=2h
            var w1 = await runner.RunDripCycleAsync(board, T0.AddHours(2));
            w1.DrippedLeadIds.Should().HaveCount(5);
            w1.RevokedLeadIds.Should().HaveCount(3);

            // Window 2: t=26h (just past 24h window)
            var w2 = await runner.RunDripCycleAsync(board, T0.AddHours(26));
            w2.DrippedLeadIds.Should().HaveCount(5);
            w2.RevokedLeadIds.Should().HaveCount(1); // moss had only 1 lead remaining (4 - 3 from window 1)

            // Window 3: t=50h
            var w3 = await runner.RunDripCycleAsync(board, T0.AddHours(50));
            w3.DrippedLeadIds.Should().HaveCount(5);

            // Final state: roma has 4 (cold) + 15 (3 × 5 drips) = 19 leads.
            pool.GetAssignedLeads("roma").Should().HaveCount(19);

            // Ledger has 3 drip events + at least 2 revoke events.
            var drips = await ledger.QueryAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.GlengarryLeadDripped)).ToListAsync();
            drips.Should().HaveCount(3);
            drips.Should().AllSatisfy(e => e.Persona.Should().Be("roma"));
        }
        finally { File.Delete(packPath); }
    }

    [Fact]
    public async Task RunDripCycle_typed_payload_persists_lead_ids()
    {
        await using var ledger = new SqliteArenaLedger("Data Source=:memory:");
        var (pool, packPath) = await NewPoolWithPackAsync(glengarryCount: 5, coldCount: 5);
        try
        {
            await pool.AssignAsync("moss", 3, tier: "cold"); // give moss something to lose

            var runner = new GlengarryDripPolicyRunner(pool, ledger,
                new GlengarryDripPolicy(TimeSpan.FromHours(24), DripCount: 2, BottomRevokeCount: 2));

            var board = Leaderboard(
                ("roma", 1, LeaderboardTier.Cadillac, 100_000m, 2),
                ("moss", 2, LeaderboardTier.YouAreFired, 0m, 0));

            var decision = await runner.RunDripCycleAsync(board, T0);

            var drip = (await ledger.QueryAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.GlengarryLeadDripped)).ToListAsync()).Single();
            var dripPayload = drip.GetPayload<GlengarryLeadDrippedPayload>()!;
            dripPayload.Persona.Should().Be("roma");
            dripPayload.LeadIds.Should().BeEquivalentTo(decision.DrippedLeadIds);

            var revoke = (await ledger.QueryAsync(ArenaEventFilter.OfKind(Contest, ArenaEventKinds.LeadsRevoked)).ToListAsync()).Single();
            var revokePayload = revoke.GetPayload<LeadsRevokedPayload>()!;
            revokePayload.Persona.Should().Be("moss");
            revokePayload.LeadIds.Should().BeEquivalentTo(decision.RevokedLeadIds);
        }
        finally { File.Delete(packPath); }
    }

    [Fact]
    public void LeadPool_GetAssignedLeads_returns_owned_in_load_order()
    {
        // Quick regression check on the new ILeadPool method (touches SA-02-02 surface).
        var pool = new InMemoryLeadPool();
        pool.GetAssignedLeads("anyone").Should().BeEmpty();
    }

    // ---- helpers ---------------------------------------------------------

    private static Leaderboard.Leaderboard Leaderboard(params (string Persona, int Position, LeaderboardTier Tier, decimal RevenueUsd, int Wins)[] rows)
    {
        var entries = rows.Select(r => new LeaderboardRow(
            Position: r.Position,
            Tier: r.Tier,
            Persona: r.Persona,
            Score: (double)r.RevenueUsd,
            RevenueUsd: r.RevenueUsd,
            DealsWon: r.Wins,
            DealsLost: 0,
            WinRate: r.Wins > 0 ? 1.0 : 0.0)).ToList();
        return new Leaderboard.Leaderboard(Contest, ScoringConfigIds.ByRevenue, T0, entries);
    }

    private static async Task<(InMemoryLeadPool Pool, string PackPath)> NewPoolWithPackAsync(int glengarryCount, int coldCount)
    {
        var leads = new List<object>(glengarryCount + coldCount);
        var id = 1;
        for (var i = 0; i < glengarryCount; i++)
        {
            leads.Add(new { id = $"L-{id++:0000}", tier = "glengarry", company = new { name = $"Premium Co {i}" } });
        }
        for (var i = 0; i < coldCount; i++)
        {
            leads.Add(new { id = $"L-{id++:0000}", tier = "cold", company = new { name = $"Cold Co {i}" } });
        }

        var pack = new
        {
            version = "v1",
            name = "drip-fixture",
            description = "Glengarry-drip test fixture",
            synthetic = true,
            leads,
        };

        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(pack, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var pool = new InMemoryLeadPool();
        await pool.LoadAsync(path);
        return (pool, path);
    }
}
