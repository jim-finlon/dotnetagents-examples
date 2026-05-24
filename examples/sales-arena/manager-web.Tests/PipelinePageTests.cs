using System.Security.Claims;
using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Auth;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Services;
using SalesArena.Manager.Web.Services.Pipeline;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class PipelinePageTests : TestContext
{
    public PipelinePageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<PipelineFunnelState>();
        Services.AddSingleton<ArenaLiveFeed>();
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Pipeline_renders_funnel_stages_and_scoreboard()
    {
        var state = Services.GetRequiredService<PipelineFunnelState>();
        SeedDemoEvents(state);

        var cut = RenderComponent<Pipeline>();
        cut.WaitForState(() => cut.FindAll("[data-testid='pipeline-stage']").Count >= 9);

        cut.Find("[data-testid='pipeline-scoreboard']");
        Assert.Contains("Lead", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Closed", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Pipeline_applies_deal_closed_increases_revenue()
    {
        var state = Services.GetRequiredService<PipelineFunnelState>();
        state.ApplyNewEvents(
        [
            Deal("roma", "L-900", 50_000m),
        ]);

        var cut = RenderComponent<Pipeline>();
        cut.WaitForState(() => cut.Markup.Contains("$50,000", StringComparison.Ordinal));
        Assert.Contains("RO", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pipeline_persona_markers_follow_touch_events()
    {
        var state = Services.GetRequiredService<PipelineFunnelState>();
        state.ApplyNewEvents(
        [
            Touch("moss", "L-1"),
        ]);

        var cut = RenderComponent<Pipeline>();
        cut.WaitForState(() => cut.FindAll("[data-testid='pipeline-persona']").Count > 0);
        Assert.Contains("MO", cut.Markup, StringComparison.Ordinal);
    }

    private static void SeedDemoEvents(PipelineFunnelState state)
    {
        state.ApplyNewEvents(
        [
            Touch("roma", "L-100"),
            Touch("levene", "L-101"),
            Deal("roma", "L-100", 25_000m),
        ]);
    }

    private static ArenaEventMessage Touch(string persona, string leadId) =>
        new(1, "demo", ArenaEventKinds.TouchSent, DateTimeOffset.UtcNow, leadId, persona, "{}");

    private static ArenaEventMessage Deal(string persona, string leadId, decimal value) =>
        new(
            2,
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
