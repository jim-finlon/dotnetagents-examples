using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Models;
using SalesArena.Manager.Web.Services;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class FloorViewStateTests
{
    [Fact]
    public void EnsureDefaultPods_seeds_four_personas()
    {
        var state = new FloorViewState();
        state.EnsureDefaultPods();

        Assert.Equal(4, state.Personas.Count);
        Assert.Contains(state.Personas, p => p.PersonaId == "roma");
    }

    [Fact]
    public void ApplyNewEvents_updates_ticker_and_tier_from_snapshot()
    {
        var state = new FloorViewState();
        state.EnsureDefaultPods();

        var snapshot = new ArenaEventMessage(
            2,
            "demo",
            ArenaEventKinds.LeaderboardSnapshot,
            DateTimeOffset.UtcNow,
            null,
            null,
            ArenaEvent.SerializePayload(new LeaderboardSnapshotPayload(
            [
                new LeaderboardEntry("levene", 1, LeaderboardTierNames.Cadillac, 90_000m, 2, 0, 0.5),
            ],
            "demo")));

        state.ApplyNewEvents([snapshot]);

        var levene = state.Personas.Single(p => p.PersonaId == "levene");
        Assert.Equal(FloorTier.Cadillac, levene.Tier);
        Assert.NotEmpty(levene.TickerLines);
    }

    [Fact]
    public void ApplyNewEvents_increments_deals_on_deal_closed()
    {
        var state = new FloorViewState();
        state.EnsureDefaultPods();

        var deal = new ArenaEventMessage(
            3,
            "demo",
            ArenaEventKinds.DealClosed,
            DateTimeOffset.UtcNow,
            "L-9",
            "roma",
            ArenaEvent.SerializePayload(new DealClosedPayload("L-9", "roma", "Won", 10_000m, null)));

        state.ApplyNewEvents([deal]);

        var roma = state.Personas.Single(p => p.PersonaId == "roma");
        Assert.Equal(1, roma.DealsToday);
        Assert.True(roma.PulseDealClosed);
        Assert.Contains(roma.TickerLines, line => line.Contains("Deal closed", StringComparison.OrdinalIgnoreCase));
    }
}
