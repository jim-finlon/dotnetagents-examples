using System;
using FluentAssertions;
using Xunit;

namespace SalesArena.Ops.Tests;

public class OpsAgentSetupContestTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static OpsAgent Create() => new(() => FixedNow);

    [Fact]
    public void SetupContest_HappyPath_PopulatesPlan()
    {
        var agent = Create();

        var plan = agent.SetupContest(new ContestRequest(
            Name: "Glengarry Q2",
            PersonaIds: new[] { "roma", "levene", "moss" },
            DurationHours: 1.0,
            PrizeTier: "cadillac"));

        plan.Name.Should().Be("Glengarry Q2");
        plan.StartUtc.Should().Be(FixedNow);
        plan.EndUtc.Should().Be(FixedNow.AddHours(1.0));
        plan.PersonaSeatCount.Should().Be(3);
        plan.PrizeTier.Should().Be("cadillac");
        plan.PersonaIds.Should().Equal(new[] { "roma", "levene", "moss" });
    }

    [Fact]
    public void SetupContest_EmptyName_Rejected()
    {
        var agent = Create();

        var act = () => agent.SetupContest(new ContestRequest(
            Name: "   ",
            PersonaIds: new[] { "roma" },
            DurationHours: 1.0,
            PrizeTier: "cadillac"));

        act.Should().Throw<ArgumentException>().WithMessage("*name*");
    }

    [Fact]
    public void SetupContest_EmptyPersonas_Rejected()
    {
        var agent = Create();

        var act = () => agent.SetupContest(new ContestRequest(
            Name: "Glengarry Q2",
            PersonaIds: Array.Empty<string>(),
            DurationHours: 1.0,
            PrizeTier: "cadillac"));

        act.Should().Throw<ArgumentException>().WithMessage("*persona*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.5)]
    public void SetupContest_NonPositiveHours_Rejected(double hours)
    {
        var agent = Create();

        var act = () => agent.SetupContest(new ContestRequest(
            Name: "Glengarry Q2",
            PersonaIds: new[] { "roma" },
            DurationHours: hours,
            PrizeTier: "cadillac"));

        act.Should().Throw<ArgumentException>().WithMessage("*DurationHours*");
    }

    [Fact]
    public void SetupContest_BlankPrizeTier_DefaultsToStandard()
    {
        var agent = Create();

        var plan = agent.SetupContest(new ContestRequest(
            Name: "Glengarry Q2",
            PersonaIds: new[] { "roma" },
            DurationHours: 1.0,
            PrizeTier: ""));

        plan.PrizeTier.Should().Be("standard");
    }

    [Fact]
    public void SetupContest_OnlyBlankPersonas_Rejected()
    {
        var agent = Create();

        var act = () => agent.SetupContest(new ContestRequest(
            Name: "Glengarry Q2",
            PersonaIds: new[] { "", "   " },
            DurationHours: 1.0,
            PrizeTier: "cadillac"));

        act.Should().Throw<ArgumentException>().WithMessage("*non-blank persona*");
    }

    [Fact]
    public void SetupContest_ExplicitStartUtc_PreservedInPlan()
    {
        var agent = Create();
        var explicitStart = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

        var plan = agent.SetupContest(new ContestRequest(
            Name: "Glengarry Q3",
            PersonaIds: new[] { "roma" },
            DurationHours: 2.0,
            PrizeTier: "cadillac",
            StartUtc: explicitStart));

        plan.StartUtc.Should().Be(explicitStart);
        plan.EndUtc.Should().Be(explicitStart.AddHours(2.0));
    }
}
