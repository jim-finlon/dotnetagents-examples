using System;
using System.Collections.Generic;
using FluentAssertions;
using SalesArena.Orchestrator.Contest;
using Xunit;

namespace SalesArena.Orchestrator.Contest.Tests;

public class RulesEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeContext : IContestRuleEvaluationContext
    {
        public ContestState State { get; set; } = new(
            "c1", "Glengarry", ContestPhase.Running, T0, null, null, TimeSpan.Zero,
            new[] { "roma", "levene", "moss" }, 1.0, "cadillac");

        public int OutboundSendCapPerHour { get; set; } = 50;
        public IReadOnlyCollection<(int StartHour, int EndHourExclusive)> BlackoutWindowsUtc { get; set; }
            = new[] { (22, 24), (0, 7) };
        public HashSet<(string LeadId, string PersonaId)> Touches { get; } = new();

        public bool HasLeadBeenTouchedByOtherPersona(string leadId, string currentPersonaId)
        {
            foreach (var (lid, pid) in Touches)
                if (lid == leadId && pid != currentPersonaId) return true;
            return false;
        }
    }

    [Fact]
    public void DefaultEngine_HasFiveStarterRules()
    {
        var engine = new RulesEngine();
        engine.Rules.Should().HaveCount(5);
    }

    [Fact]
    public void NoDoubleTouch_TriggersOnSecondPersona()
    {
        var ctx = new FakeContext();
        ctx.Touches.Add(("L1", "roma"));
        var engine = new RulesEngine();

        var v = engine.Evaluate(new LeadTouchedEvent("c1", T0, "moss", "L1"), ctx);

        v.Should().NotBeNull();
        v!.RuleId.Should().Be("no-double-touch");
    }

    [Fact]
    public void NoDoubleTouch_AllowsSamePersonaRepeat()
    {
        var ctx = new FakeContext();
        ctx.Touches.Add(("L1", "roma"));
        var engine = new RulesEngine();

        var v = engine.Evaluate(new LeadTouchedEvent("c1", T0, "roma", "L1"), ctx);

        v.Should().BeNull();
    }

    [Fact]
    public void SendRateCap_TriggersAboveCap()
    {
        var ctx = new FakeContext { OutboundSendCapPerHour = 10 };
        var engine = new RulesEngine();

        var v = engine.Evaluate(new OutboundSendEvent("c1", T0, "moss", SendCountInWindow: 11), ctx);

        v!.RuleId.Should().Be("send-rate-cap");
    }

    [Fact]
    public void SendRateCap_AllowsAtCap()
    {
        var ctx = new FakeContext { OutboundSendCapPerHour = 10 };
        var engine = new RulesEngine();

        var v = engine.Evaluate(new OutboundSendEvent("c1", T0, "moss", SendCountInWindow: 10), ctx);

        v.Should().BeNull();
    }

    [Fact]
    public void BlackoutHours_TriggersInsideWindow()
    {
        var ctx = new FakeContext();
        var engine = new RulesEngine();
        var midnightUtc = new DateTimeOffset(2026, 5, 18, 23, 30, 0, TimeSpan.Zero);

        var v = engine.Evaluate(new OutboundSendEvent("c1", midnightUtc, "moss", 1), ctx);

        v!.RuleId.Should().Be("blackout-hours");
    }

    [Fact]
    public void BlackoutHours_AllowsDaytime()
    {
        var ctx = new FakeContext();
        var engine = new RulesEngine();
        var noonUtc = new DateTimeOffset(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

        var v = engine.Evaluate(new OutboundSendEvent("c1", noonUtc, "moss", 1), ctx);

        v.Should().BeNull();
    }

    [Fact]
    public void ScoringLockedMidContest_BlocksWhileRunning()
    {
        var ctx = new FakeContext();
        var engine = new RulesEngine();

        var v = engine.Evaluate(new ScoringConfigChangeAttempt("c1", T0), ctx);

        v!.RuleId.Should().Be("scoring-locked-mid-contest");
    }

    [Fact]
    public void ScoringLockedMidContest_AllowsBeforeStart()
    {
        var ctx = new FakeContext
        {
            State = new ContestState("c1", "x", ContestPhase.Initialized, null, null, null,
                TimeSpan.Zero, Array.Empty<string>(), 1.0, "standard"),
        };
        var engine = new RulesEngine();

        var v = engine.Evaluate(new ScoringConfigChangeAttempt("c1", T0), ctx);

        v.Should().BeNull();
    }

    [Fact]
    public void PersonaActiveSetLocked_BlocksRunningChange()
    {
        var ctx = new FakeContext();
        var engine = new RulesEngine();

        var v = engine.Evaluate(new ActivePersonaSetChangeAttempt("c1", T0,
            new[] { "roma", "moss" }), ctx);

        v!.RuleId.Should().Be("persona-active-set-locked");
    }

    [Fact]
    public void PersonaActiveSetLocked_AllowsIdenticalSetCalls()
    {
        var ctx = new FakeContext();
        var engine = new RulesEngine();

        var v = engine.Evaluate(new ActivePersonaSetChangeAttempt("c1", T0,
            new[] { "roma", "levene", "moss" }), ctx);

        v.Should().BeNull();
    }
}
