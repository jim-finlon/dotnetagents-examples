using FluentAssertions;
using SalesArena.Seasons;
using Xunit;

namespace SalesArena.Seasons.Tests;

public sealed class InMemoryEloRatingTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static MatchRecord Match(string a, string b, MatchOutcome outcome, string season)
        => new(a, b, outcome, MatchId: Guid.NewGuid().ToString("N"), SeasonId: season, PlayedAtUtc: _t0);

    [Fact]
    public void GetRating_returns_default_for_unknown_persona()
    {
        var elo = new InMemoryEloRating();
        elo.GetRating("season-1", "newcomer").Should().Be(EloCalculator.DefaultStartingRating);
    }

    [Fact]
    public void ApplyMatch_updates_both_personas_in_season()
    {
        var elo = new InMemoryEloRating();
        elo.ApplyMatch(Match("roma", "moss", MatchOutcome.AWins, "s-1"));

        elo.GetRating("s-1", "roma").Should().BeApproximately(1516.0, 1e-6);
        elo.GetRating("s-1", "moss").Should().BeApproximately(1484.0, 1e-6);
        elo.GetRating("s-2", "roma").Should().Be(EloCalculator.DefaultStartingRating);
    }

    [Fact]
    public void Leaderboard_orders_by_display_rating_desc_with_ordinal_tiebreak()
    {
        var elo = new InMemoryEloRating();
        elo.ApplyMatch(Match("roma", "moss", MatchOutcome.AWins, "s-1"));   // roma 1516, moss 1484
        elo.ApplyMatch(Match("levene", "aaronow", MatchOutcome.Draw, "s-1")); // both 1500

        var board = elo.GetLeaderboard("s-1");
        board.Select(e => e.Persona).Should().BeEquivalentTo(
            new[] { "roma", "aaronow", "levene", "moss" }, opts => opts.WithStrictOrdering());
        board[0].Position.Should().Be(1);
        board[^1].Position.Should().Be(4);
    }

    [Fact]
    public void Season_buffs_lift_display_rating_without_changing_raw()
    {
        var elo = new InMemoryEloRating();
        elo.ApplyMatch(Match("roma", "moss", MatchOutcome.AWins, StarterSeasons.GlengarryId)); // roma 1516
        var glengarry = StarterSeasons.Glengarry(_t0);

        var board = elo.GetLeaderboard(StarterSeasons.GlengarryId, glengarry);
        var roma = board.Single(e => e.Persona == "roma");
        roma.RawRating.Should().BeApproximately(1516.0, 1e-6);
        roma.DisplayRating.Should().BeApproximately(1516.0 + 50.0, 1e-6, "Glengarry buffs roma +50");
    }

    [Fact]
    public void Burnout_penalty_caps_overworked_personas_under_office_space_theme()
    {
        var elo = new InMemoryEloRating();
        elo.ApplyMatch(Match("aaronow", "levene", MatchOutcome.AWins, StarterSeasons.OfficeSpaceId)); // aaronow 1516
        var office = StarterSeasons.OfficeSpaceRelaxed(_t0);

        var workedHard = new Dictionary<string, TimeSpan>(StringComparer.Ordinal)
        {
            ["aaronow"] = TimeSpan.FromHours(12), // 4h over the 8h threshold
        };

        var board = elo.GetLeaderboard(StarterSeasons.OfficeSpaceId, office, workedHard);
        var aaronow = board.Single(e => e.Persona == "aaronow");

        var expectedDisplay = 1516.0 + 25.0 /* office-space buff */ - (4 * 15.0) /* burnout penalty */;
        aaronow.DisplayRating.Should().BeApproximately(expectedDisplay, 1e-6);
    }

    [Fact]
    public void AllTime_aggregates_across_seasons()
    {
        var elo = new InMemoryEloRating();
        elo.ApplyMatch(Match("roma", "moss", MatchOutcome.AWins, "s-1")); // roma 1516
        elo.ApplyMatch(Match("roma", "moss", MatchOutcome.BWins, "s-2")); // roma 1484

        var allTime = elo.GetRating(SeasonScopes.AllTime, "roma");
        allTime.Should().BeApproximately(1500.0, 1e-6);
    }

    [Fact]
    public void AllTime_leaderboard_ignores_theme_buffs()
    {
        var elo = new InMemoryEloRating();
        elo.ApplyMatch(Match("roma", "moss", MatchOutcome.AWins, StarterSeasons.GlengarryId));
        var glengarry = StarterSeasons.Glengarry(_t0);

        var board = elo.GetLeaderboard(SeasonScopes.AllTime, glengarry);
        var roma = board.Single(e => e.Persona == "roma");
        roma.DisplayRating.Should().Be(roma.RawRating, "all-time view should not apply seasonal buffs");
    }

    [Fact]
    public void MatchRecord_without_season_is_rejected()
    {
        var elo = new InMemoryEloRating();
        var match = new MatchRecord("a", "b", MatchOutcome.AWins, "m-1", SeasonId: null, PlayedAtUtc: _t0);
        Action act = () => elo.ApplyMatch(match);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyMatch_increments_match_count_per_persona()
    {
        var elo = new InMemoryEloRating();
        elo.ApplyMatch(Match("roma", "moss", MatchOutcome.AWins, "s-1"));
        elo.ApplyMatch(Match("roma", "levene", MatchOutcome.AWins, "s-1"));

        var board = elo.GetLeaderboard("s-1");
        board.Single(e => e.Persona == "roma").MatchesPlayed.Should().Be(2);
        board.Single(e => e.Persona == "moss").MatchesPlayed.Should().Be(1);
        board.Single(e => e.Persona == "levene").MatchesPlayed.Should().Be(1);
    }

    [Fact]
    public void Custom_K_factor_changes_swing()
    {
        var lowK = new InMemoryEloRating(kFactor: 8);
        lowK.ApplyMatch(Match("a", "b", MatchOutcome.AWins, "s-1"));
        lowK.GetRating("s-1", "a").Should().BeApproximately(1504.0, 1e-6);

        var highK = new InMemoryEloRating(kFactor: 64);
        highK.ApplyMatch(Match("a", "b", MatchOutcome.AWins, "s-1"));
        highK.GetRating("s-1", "a").Should().BeApproximately(1532.0, 1e-6);
    }
}
