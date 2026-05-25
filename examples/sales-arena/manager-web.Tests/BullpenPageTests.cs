using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Services;
using SalesArena.Manager.Web.Services.Bullpen;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class BullpenPageTests : TestContext
{
    public BullpenPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<ArenaLiveFeed>();
        Services.AddSingleton<BullpenCamState>();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Bullpen_renders_tiles_for_default_personas()
    {
        var state = Services.GetRequiredService<BullpenCamState>();
        state.EnsureDefaultTiles();

        var cut = RenderComponent<Bullpen>();
        var tiles = cut.FindAll("[data-testid='bullpen-tile']");
        Assert.Equal(4, tiles.Count);
        cut.Find("[data-testid='bullpen-page']");
    }

    [Fact]
    public void Bullpen_shows_sanitized_thought_after_ledger_event()
    {
        var state = Services.GetRequiredService<BullpenCamState>();
        state.EnsureDefaultTiles();
        state.ApplyNewEvents([
            new ArenaEventMessage(
                1,
                "demo",
                ArenaEventKinds.DealClosed,
                DateTimeOffset.UtcNow,
                "L-1",
                "moss",
                ArenaEvent.SerializePayload(new DealClosedPayload("L-1", "moss", "Won", 120_000m, null))),
        ]);

        var cut = RenderComponent<Bullpen>();
        var thought = cut.Find("[data-persona='moss'] [data-testid='bullpen-thought']").TextContent;
        Assert.Contains("$100K", thought, StringComparison.Ordinal);
        Assert.DoesNotContain("120000", thought, StringComparison.Ordinal);
    }

    [Fact]
    public void Bullpen_activity_label_updates_on_touch()
    {
        var state = Services.GetRequiredService<BullpenCamState>();
        state.EnsureDefaultTiles();
        state.ApplyNewEvents([
            new ArenaEventMessage(
                2,
                "demo",
                ArenaEventKinds.TouchSent,
                DateTimeOffset.UtcNow,
                "L-2",
                "aaronow",
                ArenaEvent.SerializePayload(new TouchSentPayload("L-2", "aaronow", "linkedin", "t1", "v1", null, 80))),
        ]);

        var cut = RenderComponent<Bullpen>();
        var activity = cut.Find("[data-persona='aaronow'] [data-testid='bullpen-activity']").TextContent;
        Assert.Contains("Sending", activity, StringComparison.Ordinal);
    }
}
