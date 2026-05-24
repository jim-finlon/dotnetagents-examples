using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Models;
using SalesArena.Manager.Web.Services.Bullpen;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class BullpenCamStateTests
{
    [Fact]
    public void EnsureDefaultTiles_seeds_four_personas()
    {
        var state = new BullpenCamState();
        state.EnsureDefaultTiles();

        Assert.Equal(4, state.Tiles.Count);
        Assert.Contains(state.Tiles, t => t.PersonaId == "roma");
    }

    [Fact]
    public void ApplyNewEvents_updates_activity_and_thought_on_touch()
    {
        var state = new BullpenCamState();
        state.EnsureDefaultTiles();

        var touch = new ArenaEventMessage(
            1,
            "demo",
            ArenaEventKinds.TouchSent,
            DateTimeOffset.UtcNow,
            "L-1",
            "roma",
            ArenaEvent.SerializePayload(new TouchSentPayload("L-1", "roma", "email", "t1", "v1", "Hello Acme Corp", 120)));

        state.ApplyNewEvents([touch]);

        var roma = state.Tiles.Single(t => t.PersonaId == "roma");
        Assert.Equal(FloorActivity.Sending, roma.Activity);
        Assert.DoesNotContain("Acme", roma.CurrentThought, StringComparison.Ordinal);
        Assert.Contains("email", roma.CurrentThought, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyNewEvents_buckets_revenue_on_deal_closed()
    {
        var state = new BullpenCamState();
        state.EnsureDefaultTiles();

        var deal = new ArenaEventMessage(
            2,
            "demo",
            ArenaEventKinds.DealClosed,
            DateTimeOffset.UtcNow,
            "L-9",
            "levene",
            ArenaEvent.SerializePayload(new DealClosedPayload("L-9", "levene", "Won", 42_000m, null)));

        state.ApplyNewEvents([deal]);

        var levene = state.Tiles.Single(t => t.PersonaId == "levene");
        Assert.Contains("$10K", levene.CurrentThought, StringComparison.Ordinal);
        Assert.DoesNotContain("42000", levene.CurrentThought, StringComparison.Ordinal);
    }
}
