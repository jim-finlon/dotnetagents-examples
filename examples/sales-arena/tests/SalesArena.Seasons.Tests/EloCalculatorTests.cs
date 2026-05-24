using FluentAssertions;
using SalesArena.Seasons;
using Xunit;

namespace SalesArena.Seasons.Tests;

public sealed class EloCalculatorTests
{
    [Fact]
    public void Expected_returns_half_for_equal_ratings()
    {
        EloCalculator.Expected(1500, 1500).Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void Expected_favors_higher_rated_player()
    {
        var higher = EloCalculator.Expected(1700, 1500);
        higher.Should().BeGreaterThan(0.5);
        var lower = EloCalculator.Expected(1300, 1500);
        lower.Should().BeLessThan(0.5);
    }

    [Fact]
    public void Apply_equal_rating_win_yields_classic_16_point_swing()
    {
        // Standard chess result: 1500 vs 1500 + A wins, K=32 => A gains 16, B loses 16.
        var (newA, newB) = EloCalculator.Apply(1500, 1500, MatchOutcome.AWins);
        newA.Should().BeApproximately(1516.0, 1e-6);
        newB.Should().BeApproximately(1484.0, 1e-6);
    }

    [Fact]
    public void Apply_underdog_win_yields_larger_swing_than_favorite_win()
    {
        var (underdogWinsA, underdogWinsB) = EloCalculator.Apply(1300, 1700, MatchOutcome.AWins);
        var (favoriteWinsA, favoriteWinsB) = EloCalculator.Apply(1700, 1300, MatchOutcome.AWins);

        var underdogSwing = Math.Abs(underdogWinsA - 1300);
        var favoriteSwing = Math.Abs(favoriteWinsA - 1700);
        underdogSwing.Should().BeGreaterThan(favoriteSwing);
    }

    [Fact]
    public void Apply_draw_drifts_higher_rated_down_and_lower_up()
    {
        var (newA, newB) = EloCalculator.Apply(1700, 1500, MatchOutcome.Draw);
        newA.Should().BeLessThan(1700);
        newB.Should().BeGreaterThan(1500);
    }

    [Fact]
    public void Apply_rejects_zero_or_negative_K()
    {
        Action zero = () => EloCalculator.Apply(1500, 1500, MatchOutcome.AWins, k: 0);
        zero.Should().Throw<ArgumentOutOfRangeException>();
        Action neg = () => EloCalculator.Apply(1500, 1500, MatchOutcome.AWins, k: -5);
        neg.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Apply_rejects_unknown_outcome()
    {
        Action act = () => EloCalculator.Apply(1500, 1500, (MatchOutcome)999);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
