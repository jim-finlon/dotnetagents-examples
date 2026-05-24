using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Services.MoneyMap;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class MoneyMapStateTests
{
    [Fact]
    public void PinRadius_scales_with_deal_value()
    {
        var small = MoneyMapState.PinRadius(5_000m);
        var large = MoneyMapState.PinRadius(250_000m);
        Assert.True(large > small);
        Assert.InRange(small, 6, 28);
        Assert.InRange(large, 6, 28);
    }

    [Fact]
    public void Apply_deal_closed_adds_us_pin_with_region()
    {
        var env = new TestWebHostEnvironment();
        var state = new MoneyMapState(
            new RegionCoordinateCatalog(env),
            new PersonaDisplayColorCatalog(env),
            new MoneyMapGeoJsonPaths(env));

        state.ApplyNewEvents(
        [
            Deal("roma", "L-1002", 75_000m, id: 1),
        ]);

        var snapshot = state.Snapshot;
        Assert.Equal(1, snapshot.UsPins.Count + snapshot.WorldPins.Count);
        Assert.Equal(75_000m, snapshot.TotalRevenueUsd);
        var pin = snapshot.UsPins.Count > 0 ? snapshot.UsPins[0] : snapshot.WorldPins[0];
        Assert.Equal("#C41E3A", pin.PersonaColor);
        Assert.Equal("roma", pin.Persona, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Apply_ignores_lost_deals_and_duplicate_leads()
    {
        var env = new TestWebHostEnvironment();
        var state = new MoneyMapState(
            new RegionCoordinateCatalog(env),
            new PersonaDisplayColorCatalog(env),
            new MoneyMapGeoJsonPaths(env));

        state.ApplyNewEvents(
        [
            Deal("levene", "L-200", 10_000m, id: 1, outcome: "Lost"),
            Deal("levene", "L-200", 22_000m, id: 2),
            Deal("levene", "L-200", 99_000m, id: 3),
        ]);

        Assert.Single(state.Snapshot.UsPins);
        Assert.Equal(22_000m, state.Snapshot.UsPins[0].ValueUsd);
    }

    private static ArenaEventMessage Deal(
        string persona,
        string leadId,
        decimal value,
        long id,
        string outcome = "Won") =>
        new(
            id,
            "demo",
            ArenaEventKinds.DealClosed,
            DateTimeOffset.UtcNow,
            leadId,
            persona,
            ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, outcome, value, null)));

    public sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public TestWebHostEnvironment()
        {
            WebRootPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "manager-web", "SalesArena.Manager.Web", "wwwroot"));
            ContentRootPath = WebRootPath;
        }

        public string WebRootPath { get; set; }

        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string EnvironmentName { get; set; } = "Development";
    }
}
