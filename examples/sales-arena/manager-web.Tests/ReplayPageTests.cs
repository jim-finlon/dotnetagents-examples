using Bunit;
using DotNetAgents.Ui.Blazor;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Manager.Web.Services.Replay;
using ReplayPage = SalesArena.Manager.Web.Components.Pages.Replay;
using SalesArena.Replay;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class ReplayPageTests : TestContext
{
    public ReplayPageTests()
    {
        Services.AddDotNetAgentsUi();
        Services.AddSingleton<IReplayBrowserService>(new StubReplayBrowserService());
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public void Replay_renders_contest_list_and_section_markdown()
    {
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo("/replay?contestId=demo-contest");

        var cut = RenderComponent<ReplayPage>();

        cut.WaitForState(() => cut.Markup.Contains("Tuesday Steak-Knives", StringComparison.Ordinal));
        Assert.NotEmpty(cut.FindAll(".sa-replay__contest"));
        cut.WaitForState(() => cut.Markup.Contains("roma", StringComparison.Ordinal));
        Assert.NotEmpty(cut.FindAll(".sa-replay__section-chip"));
    }

    [Fact]
    public void Replay_deep_link_shows_deal_focus_timeline()
    {
        var nav = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        nav.NavigateTo("/replay?contestId=demo-contest&leadId=L-1002");

        var cut = RenderComponent<ReplayPage>();

        cut.WaitForState(() => cut.Markup.Contains("roma sent email", StringComparison.Ordinal));
        Assert.NotEmpty(cut.FindAll(".sa-replay__timeline li"));
    }

    private sealed class StubReplayBrowserService : IReplayBrowserService
    {
        public Task<IReadOnlyList<ReplayContestSummary>> ListContestsAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReplayContestSummary> contests =
            [
                new("demo-contest", "Tuesday Steak-Knives Bake-Off", DateTimeOffset.UtcNow, "roma", 1),
                new("archived-week-1", "Last week's Glengarry sprint", DateTimeOffset.UtcNow.AddDays(-7), "moss", 1),
            ];
            return Task.FromResult(contests);
        }

        public Task<ReplayReport> GetReportAsync(string contestId, CancellationToken cancellationToken = default)
        {
            var report = new ReplayReport(
                contestId,
                DateTimeOffset.UtcNow,
                "# Replay",
                [
                    new(ReplaySectionKind.Leaderboard, "Leaderboard", "roma | 1 | Cadillac"),
                    new(ReplaySectionKind.PersonaDealLog, "Deals", "- **L-1002** — Won"),
                ],
                [
                    new(ReplaySectionKind.ClosestCall, "Near miss", "roma", "L-1002", 10_000m, null),
                ]);
            return Task.FromResult(report);
        }

        public Task<ReplayDealFocus?> GetDealFocusAsync(
            string contestId,
            string leadId,
            ReplayReport? report = null,
            CancellationToken cancellationToken = default)
        {
            if (!string.Equals(leadId, "L-1002", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<ReplayDealFocus?>(null);
            }

            ReplayDealFocus focus = new(
                leadId,
                "roma",
                [new ReplayDealEvent(DateTimeOffset.UtcNow, "TouchSent", "roma sent email")],
                report?.Highlights ?? []);
            return Task.FromResult<ReplayDealFocus?>(focus);
        }
    }
}
