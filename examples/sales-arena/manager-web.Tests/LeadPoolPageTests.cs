using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Components.Pages;
using SalesArena.Manager.Web.Services.LeadPool;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class LeadPoolPageTests : TestContext
{
    public LeadPoolPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<ILeadPoolSnapshotProvider>(new StubLeadPoolProvider());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void LeadPool_renders_filter_chips_and_table_rows()
    {
        var cut = RenderComponent<LeadPool>();
        cut.WaitForState(() => cut.Markup.Contains("Hot Co", StringComparison.Ordinal));
        Assert.NotEmpty(cut.FindAll(".sa-lead-pool__chip"));
    }

    [Fact]
    public void LeadPool_filter_warm_hides_hot_row()
    {
        var cut = RenderComponent<LeadPool>();
        cut.WaitForState(() => cut.Markup.Contains("Hot Co", StringComparison.Ordinal));

        var warmButton = cut.FindAll(".sa-lead-pool__chip")
            .First(b => b.TextContent.Trim() == "Warm");
        cut.InvokeAsync(() =>
        {
            warmButton.Click();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        cut.WaitForState(() => cut.Markup.Contains("Warm Co", StringComparison.Ordinal));
        Assert.DoesNotContain(cut.Markup, "Hot Co", StringComparison.Ordinal);
    }

    [Fact]
    public void LeadPool_row_click_shows_drill_down_and_replay_link()
    {
        var cut = RenderComponent<LeadPool>();
        var warmChip = cut.FindAll(".sa-lead-pool__chip")
            .First(b => b.TextContent.Trim() == "Warm");
        cut.InvokeAsync(() =>
        {
            warmChip.Click();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
        cut.WaitForState(() => cut.Markup.Contains("Warm Co", StringComparison.Ordinal));

        var rowLink = cut.FindAll(".sa-lead-pool__row-link")
            .First(b => b.TextContent.Contains("Warm Co", StringComparison.Ordinal));
        cut.InvokeAsync(() =>
        {
            rowLink.Click();
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();

        cut.WaitForState(() => cut.Markup.Contains("Open in Replay", StringComparison.Ordinal));
        var replayLink = cut.Find(".sa-lead-pool__replay-link");
        Assert.Equal("/replay?contestId=demo-contest&leadId=warm-1", replayLink.GetAttribute("href"));
        var activityItems = cut.FindAll(".sa-lead-pool__activity li");
        Assert.NotEmpty(activityItems);
        Assert.Contains(activityItems, li => li.TextContent.Contains("romano touched warm-1", StringComparison.Ordinal));
    }

    private sealed class StubLeadPoolProvider : ILeadPoolSnapshotProvider
    {
        public Task<IReadOnlyList<LeadPoolLead>> GetLeadsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTimeOffset.UtcNow;
            IReadOnlyList<LeadPoolLead> leads =
            [
                new(
                    "hot-1",
                    "Hot Co",
                    "moss",
                    "Prospect",
                    80,
                    now.AddHours(-1),
                    0,
                    HasReply: true,
                    IsClosedWon: false,
                    IsClosedLost: false,
                    [new LeadPoolActivityEntry(now.AddHours(-1), "Touched hot-1")]),
                new(
                    "warm-1",
                    "Warm Co",
                    "romano",
                    "Qualified",
                    60,
                    now.AddDays(-4),
                    1,
                    HasReply: false,
                    IsClosedWon: false,
                    IsClosedLost: false,
                    [
                        new LeadPoolActivityEntry(now.AddDays(-4), "romano touched warm-1"),
                        new LeadPoolActivityEntry(now.AddDays(-5), "romano follow-up - no response"),
                    ]),
            ];

            return Task.FromResult(leads);
        }
    }
}
