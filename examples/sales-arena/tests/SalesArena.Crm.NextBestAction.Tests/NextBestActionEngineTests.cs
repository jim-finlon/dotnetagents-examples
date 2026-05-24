using System;
using FluentAssertions;
using SalesArena.Crm.NextBestAction;
using Xunit;

namespace SalesArena.Crm.NextBestAction.Tests;

public class NextBestActionEngineTests
{
    private static CrmContext Context(
        string stage = "Discovery",
        int fit = 50, int intent = 40, int power = 30,
        int days = 2,
        bool obj = false, bool meet = false,
        decimal? proposal = null) =>
        new("L1", stage, fit, intent, power, days, obj, meet, proposal);

    private static NextBestActionEngine NewEngine() => new();

    [Fact]
    public void UnknownPersona_Throws()
    {
        var act = () => NewEngine().Decide("aaronow", Context());
        act.Should().Throw<ArgumentException>().WithMessage("*aaronow*");
    }

    // --- Roma (consultative) ---

    [Fact]
    public void Roma_LowFit_Disqualifies()
    {
        var d = NewEngine().Decide("roma", Context(fit: 10));
        d.Action.Should().Be(NbaActionType.Disqualify);
    }

    [Fact]
    public void Roma_OpenObjections_FollowUp()
    {
        var d = NewEngine().Decide("roma", Context(obj: true, intent: 80));
        d.Action.Should().Be(NbaActionType.SendFollowUp);
    }

    [Fact]
    public void Roma_WarmIntent_Schedules()
    {
        var d = NewEngine().Decide("roma", Context(intent: 70));
        d.Action.Should().Be(NbaActionType.ScheduleMeeting);
    }

    [Fact]
    public void Roma_FullyQualified_WithProposal_Closes()
    {
        var d = NewEngine().Decide("roma", Context(fit: 80, intent: 75, power: 60, proposal: 50_000m));
        d.Action.Should().Be(NbaActionType.NegotiateClose);
    }

    [Fact]
    public void Roma_DefaultsTo_Discover()
    {
        var d = NewEngine().Decide("roma", Context(fit: 40, intent: 30));
        d.Action.Should().Be(NbaActionType.Discover);
    }

    // --- Levene (talker) ---

    [Fact]
    public void Levene_SilenceTriggersFollowUp()
    {
        var d = NewEngine().Decide("levene", Context(days: 9));
        d.Action.Should().Be(NbaActionType.SendFollowUp);
    }

    [Fact]
    public void Levene_AnyIntentTriggersPitch()
    {
        var d = NewEngine().Decide("levene", Context(intent: 45, days: 1));
        d.Action.Should().Be(NbaActionType.DemoOrPitch);
    }

    [Fact]
    public void Levene_LateStageProposesAgain()
    {
        var d = NewEngine().Decide("levene", Context(stage: "Negotiation", intent: 10, days: 1));
        d.Action.Should().Be(NbaActionType.SendProposal);
    }

    // --- Moss (hardballer) ---

    [Fact]
    public void Moss_LowFitDisqualifies()
    {
        var d = NewEngine().Decide("moss", Context(fit: 20, power: 40));
        d.Action.Should().Be(NbaActionType.Disqualify);
    }

    [Fact]
    public void Moss_LowPowerDisqualifies()
    {
        var d = NewEngine().Decide("moss", Context(fit: 80, power: 10));
        d.Action.Should().Be(NbaActionType.Disqualify);
    }

    [Fact]
    public void Moss_NegotiationStage_WithProposal_Closes()
    {
        var d = NewEngine().Decide("moss", Context(stage: "Negotiation", fit: 60, power: 60, proposal: 25_000m));
        d.Action.Should().Be(NbaActionType.NegotiateClose);
    }

    [Fact]
    public void Moss_PowerAndNoMeeting_ForcesMeeting()
    {
        var d = NewEngine().Decide("moss", Context(fit: 50, power: 60, meet: false));
        d.Action.Should().Be(NbaActionType.ScheduleMeeting);
    }

    // --- Williamson (rule-follower) ---

    [Fact]
    public void Williamson_DiscoveryStage_RunsDiscover()
    {
        var d = NewEngine().Decide("williamson", Context(stage: "Discovery"));
        d.Action.Should().Be(NbaActionType.Discover);
    }

    [Fact]
    public void Williamson_QualificationStage_RunsQualify()
    {
        var d = NewEngine().Decide("williamson", Context(stage: "Qualification"));
        d.Action.Should().Be(NbaActionType.Qualify);
    }

    [Fact]
    public void Williamson_DemoStage_RunsDemo()
    {
        var d = NewEngine().Decide("williamson", Context(stage: "Demo"));
        d.Action.Should().Be(NbaActionType.DemoOrPitch);
    }

    [Fact]
    public void Williamson_ProposalStage_NoProposalYet_Sends()
    {
        var d = NewEngine().Decide("williamson", Context(stage: "Proposal", proposal: null));
        d.Action.Should().Be(NbaActionType.SendProposal);
    }

    [Fact]
    public void Williamson_MeetingOnBooks_Waits()
    {
        var d = NewEngine().Decide("williamson", Context(stage: "Closed", meet: true));
        d.Action.Should().Be(NbaActionType.Wait);
    }

    [Fact]
    public void Decision_TraceIsPopulated()
    {
        var d = NewEngine().Decide("roma", Context(intent: 80));
        d.Trace.Should().NotBeEmpty();
        d.PersonaId.Should().Be("roma");
        d.Reason.Should().NotBeNullOrWhiteSpace();
    }
}
