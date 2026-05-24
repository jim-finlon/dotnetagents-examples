using FluentAssertions;
using SalesArena.Seasons;
using Xunit;

namespace SalesArena.Seasons.Tests;

public sealed class StarterSeasonsTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void All_seasons_present_and_distinct()
    {
        var all = StarterSeasons.AllAt(_t0);
        all.Select(s => s.Id).Should().BeEquivalentTo(new[]
        {
            StarterSeasons.GlengarryId,
            StarterSeasons.WolfId,
            StarterSeasons.OfficeSpaceId,
        });
        all.Select(s => s.Name).Should().OnlyHaveUniqueItems();
        all.Select(s => s.Weights).Distinct().Should().HaveCount(3, "each season has a distinct scoring shape");
    }

    [Fact]
    public void Glengarry_emphasizes_revenue()
    {
        var s = StarterSeasons.Glengarry(_t0);
        s.Weights.RevenueWeight.Should().BeGreaterThan(s.Weights.DealsWeight);
        s.Weights.RevenueWeight.Should().BeGreaterThan(s.Weights.ConversionWeight);
    }

    [Fact]
    public void Wolf_emphasizes_deal_count()
    {
        var s = StarterSeasons.WolfOfHighVolume(_t0);
        s.Weights.DealsWeight.Should().BeGreaterThan(s.Weights.RevenueWeight);
        s.Weights.DealsWeight.Should().BeGreaterThan(s.Weights.ConversionWeight);
    }

    [Fact]
    public void OfficeSpace_applies_burnout_penalty()
    {
        var s = StarterSeasons.OfficeSpaceRelaxed(_t0);
        s.Weights.BurnoutPenaltyPerHourOverThreshold.Should().BeGreaterThan(0);
        s.Weights.BurnoutPenaltyThreshold.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void Each_season_has_at_least_one_persona_buff()
    {
        var all = StarterSeasons.AllAt(_t0);
        foreach (var s in all)
        {
            s.PersonaBuffs.Should().NotBeEmpty($"{s.Name} should buff at least one persona");
        }
    }

    [Fact]
    public void Season_names_must_not_reference_copyrighted_property()
    {
        var all = StarterSeasons.AllAt(_t0);

        // Names + descriptions are stylistic only; this guard prevents an
        // accidental paste of the exact movie quote into Description or Name.
        string[] forbidden =
        {
            "Coffee is for closers",
            "Coffee's for closers",
            "Always Be Closing",
            "Sell me this pen",
            "The first rule of toxic boiler-room",
            "Boiler-Room Wolves",
        };
        foreach (var s in all)
        {
            foreach (var f in forbidden)
            {
                s.Name.Should().NotContain(f);
                s.Description.Should().NotContain(f);
            }
        }
    }
}
