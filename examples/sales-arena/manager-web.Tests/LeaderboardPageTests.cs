using Bunit;
using Microsoft.Extensions.DependencyInjection;
using SalesArena.Orchestrator.Leaderboard;
using Xunit;
using LeaderboardPage = SalesArena.Manager.Web.Components.Pages.Leaderboard;

namespace SalesArena.Manager.Web.Tests;

public sealed class LeaderboardPageTests : TestContext
{
    [Fact]
    public void Leaderboard_renders_ranked_persona_rows()
    {
        var engine = new ScriptedLeaderboardEngine(SampleBoard(
            Row(1, LeaderboardTier.Cadillac, "roma", 120_000m, 4, 1),
            Row(2, LeaderboardTier.SteakKnives, "levene", 72_000m, 2, 2),
            Row(3, LeaderboardTier.YouAreFired, "moss", 8_000m, 1, 4)));
        Services.AddSingleton<ILeaderboardEngine>(engine);

        var cut = RenderComponent<LeaderboardPage>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Roma", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Cadillac", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("Steak Knives", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("You're Fired", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("120,000", cut.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Metric_switch_recomputes_with_selected_scoring_config()
    {
        var engine = new ScriptedLeaderboardEngine(SampleBoard(
            Row(1, LeaderboardTier.Cadillac, "roma", 120_000m, 4, 1)));
        Services.AddSingleton<ILeaderboardEngine>(engine);

        var cut = RenderComponent<LeaderboardPage>();
        cut.WaitForAssertion(() => Assert.Contains(ScoringConfigIds.ByRevenue, engine.RequestedScoringIds));

        cut.Find("#leaderboard-metric").Change(ScoringConfigIds.ByDealCount);

        cut.WaitForAssertion(() =>
            Assert.Contains(ScoringConfigIds.ByDealCount, engine.RequestedScoringIds));
    }

    [Fact]
    public void Promotion_refresh_marks_changed_persona_for_animation()
    {
        var engine = new ScriptedLeaderboardEngine(
            SampleBoard(
                Row(1, LeaderboardTier.Cadillac, "levene", 80_000m, 3, 2),
                Row(2, LeaderboardTier.SteakKnives, "roma", 72_000m, 2, 1)),
            SampleBoard(
                Row(1, LeaderboardTier.Cadillac, "roma", 140_000m, 5, 1),
                Row(2, LeaderboardTier.SteakKnives, "levene", 80_000m, 3, 2)));
        Services.AddSingleton<ILeaderboardEngine>(engine);

        var cut = RenderComponent<LeaderboardPage>();
        cut.WaitForAssertion(() => Assert.Contains("Levene", cut.Markup, StringComparison.Ordinal));

        cut.Find("[data-testid='leaderboard-refresh-now']").Click();

        cut.WaitForAssertion(() =>
        {
            var roma = cut.Find("tr[data-persona='roma']");
            Assert.Equal("promoted", roma.GetAttribute("data-animation"));
            Assert.Contains("tier-promoted", roma.GetAttribute("class"), StringComparison.Ordinal);
        });
    }

    private static SalesArena.Orchestrator.Leaderboard.Leaderboard SampleBoard(params LeaderboardRow[] rows) =>
        new("demo-contest", ScoringConfigIds.ByRevenue, DateTimeOffset.Parse("2026-05-18T09:00:00Z"), rows);

    private static LeaderboardRow Row(
        int position,
        LeaderboardTier tier,
        string persona,
        decimal revenue,
        int dealsWon,
        int dealsLost)
    {
        var decisions = dealsWon + dealsLost;
        var winRate = decisions == 0 ? 0 : dealsWon / (double)decisions;
        return new LeaderboardRow(position, tier, persona, Score: (double)revenue, revenue, dealsWon, dealsLost, winRate);
    }

    private sealed class ScriptedLeaderboardEngine : ILeaderboardEngine
    {
        private readonly IReadOnlyList<SalesArena.Orchestrator.Leaderboard.Leaderboard> _boards;
        private int _callCount;

        public ScriptedLeaderboardEngine(params SalesArena.Orchestrator.Leaderboard.Leaderboard[] boards) =>
            _boards = boards;

        public List<string> RequestedScoringIds { get; } = [];

        public event EventHandler<LeaderboardChangedEventArgs>? LeaderboardChanged;

        public Task<SalesArena.Orchestrator.Leaderboard.Leaderboard> ComputeAsync(
            string contestId,
            IScoringConfig scoring,
            DateTimeOffset asOfUtc,
            CancellationToken cancellationToken = default)
        {
            RequestedScoringIds.Add(scoring.Id);
            var board = _boards[Math.Min(_callCount, _boards.Count - 1)];
            _callCount++;
            var next = board with
            {
                ContestId = contestId,
                ScoringConfigId = scoring.Id,
                AsOfUtc = asOfUtc,
            };
            LeaderboardChanged?.Invoke(this, new LeaderboardChangedEventArgs(next, next, []));
            return Task.FromResult(next);
        }
    }
}
