using System.Security.Claims;
using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Auth;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Services.ChannelPivot;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class PivotPageTests : TestContext
{
    public PivotPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<IChannelPivotService>(new StubChannelPivotService());
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider());
        Services.AddAuthorizationCore();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Pivot_renders_heatmap_and_metric_toggles()
    {
        var cut = RenderComponent<Pivot>();
        cut.WaitForState(() => cut.FindAll("[data-testid='heatmap-cell']").Count > 0);

        cut.Find("[data-testid='metric-reply-rate']");
        cut.Find("[data-testid='metric-close-rate']");
        Assert.NotEmpty(cut.FindAll("[data-testid='heatmap-cell']"));
    }

    [Fact]
    public void Pivot_drilldown_and_csv_export_work()
    {
        var cut = RenderComponent<Pivot>();
        cut.WaitForState(() => cut.FindAll("[data-testid='heatmap-cell']").Count > 0);

        cut.Find("[data-testid='heatmap-cell']").Click();
        cut.WaitForState(() => cut.Find("[data-testid='pivot-drilldown']").TextContent.Contains(
            "roma",
            StringComparison.Ordinal));
        Assert.NotEmpty(cut.FindAll("[data-testid='persona-row']"));

        cut.Find("[data-testid='export-csv']").Click();
        cut.WaitForState(() => cut.Find("[data-testid='csv-download-link']") is not null);
    }

    [Fact]
    public void Pivot_close_rate_toggle_updates_active_state()
    {
        var cut = RenderComponent<Pivot>();
        cut.WaitForState(() => cut.FindAll("[data-testid='heatmap-cell']").Count > 0);

        cut.Find("[data-testid='metric-close-rate']").Click();
        Assert.Contains("is-active", cut.Find("[data-testid='metric-close-rate']").ClassName, StringComparison.Ordinal);
    }

    private sealed class StubChannelPivotService : IChannelPivotService
    {
        private static readonly ChannelPivotSnapshot Snapshot = new(
            "demo-contest",
            DateTimeOffset.UtcNow,
            ["email", "sms"],
            ["pharma", "saas"],
            [
                new ChannelPivotCell(
                    "email",
                    "pharma",
                    6,
                    3,
                    1,
                    0.5,
                    1.0 / 6,
                    false,
                    [new ChannelPivotPersonaRow("roma", 4, 2, 1, 0.5, 0.25, true)]),
                new ChannelPivotCell(
                    "sms",
                    "saas",
                    2,
                    1,
                    0,
                    0.5,
                    0,
                    true,
                    [new ChannelPivotPersonaRow("levene", 2, 1, 0, 0.5, 0, true)]),
            ]);

        public Task<ChannelPivotSnapshot> BuildSnapshotAsync(
            string? contestId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);

        public string ExportCsv(ChannelPivotSnapshot snapshot, ChannelPivotMetricMode metricMode) =>
            "channel,industry,persona,touches,inbounds,closes,reply_rate,close_rate,low_sample,metric_mode,metric_value\n" +
            $"email,pharma,roma,4,2,1,0.5000,0.2500,false,{metricMode},0.5000\n";
    }

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
