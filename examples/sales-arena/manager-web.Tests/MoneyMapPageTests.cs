using System.Security.Claims;
using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Auth;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Services;
using SalesArena.Manager.Web.Services.MoneyMap;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class MoneyMapPageTests : TestContext
{
    public MoneyMapPageTests()
    {
        Services.AddDotNetAgentsUi();
        var env = new MoneyMapStateTests.TestWebHostEnvironment();
        Services.AddSingleton<IWebHostEnvironment>(env);
        Services.AddSingleton(env);
        Services.AddSingleton<RegionCoordinateCatalog>();
        Services.AddSingleton<PersonaDisplayColorCatalog>();
        Services.AddSingleton<MoneyMapGeoJsonPaths>();
        Services.AddSingleton<MoneyMapState>();
        Services.AddSingleton<ArenaLiveFeed>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void MoneyMap_renders_route_disclaimer_and_geojson_land()
    {
        var state = Services.GetRequiredService<MoneyMapState>();
        state.ApplyNewEvents([Deal("roma", "L-900", 42_000m)]);

        var cut = RenderComponent<MoneyMap>();
        cut.WaitForState(() => cut.Find("[data-testid='money-map-page']") is not null);

        cut.Find("[data-testid='money-map-disclaimer']");
        cut.Find("[data-testid='money-map-us-svg']");
        Assert.Contains("approximate", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MoneyMap_shows_pin_after_deal_closed()
    {
        var state = Services.GetRequiredService<MoneyMapState>();
        state.ApplyNewEvents([Deal("moss", "L-501", 18_000m)]);

        var cut = RenderComponent<MoneyMap>();
        cut.WaitForState(() => cut.FindAll("[data-testid='money-map-pin']").Count > 0);
        Assert.Contains("$18,000", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void MoneyMap_geojson_paths_render_svg_land()
    {
        var paths = Services.GetRequiredService<MoneyMapGeoJsonPaths>();
        Assert.False(string.IsNullOrWhiteSpace(paths.UsPath));
        Assert.Contains("M ", paths.UsPath, StringComparison.Ordinal);

        var cut = RenderComponent<MoneyMap>();
        cut.WaitForState(() => cut.Markup.Contains("sa-money-map__land", StringComparison.Ordinal));
    }

    private static ArenaEventMessage Deal(string persona, string leadId, decimal value) =>
        new(
            1,
            "demo",
            ArenaEventKinds.DealClosed,
            DateTimeOffset.UtcNow,
            leadId,
            persona,
            ArenaEvent.SerializePayload(new DealClosedPayload(leadId, persona, "Won", value, null)));

    private sealed class TestAuthStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "manager")],
                ManagerIdentityDefaults.Scheme);
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }
}
