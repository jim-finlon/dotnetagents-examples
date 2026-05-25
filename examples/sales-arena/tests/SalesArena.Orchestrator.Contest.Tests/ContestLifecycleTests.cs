using System;
using FluentAssertions;
using SalesArena.Orchestrator.Contest;
using Xunit;

namespace SalesArena.Orchestrator.Contest.Tests;

public class ContestLifecycleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static ContestLifecycle Create(DateTimeOffset? now = null)
        => new(() => now ?? T0);

    private static ContestConfig SampleConfig(double duration = 1.0, double tcf = 60.0) => new(
        Name: "Glengarry Q2",
        LeadsPackRef: "leads/q2.json",
        PersonaIds: new[] { "roma", "levene", "moss" },
        DurationHours: duration,
        PrizeTier: "cadillac",
        TimeCompressionFactor: tcf);

    [Fact]
    public void FullLifecycle_Smoke()
    {
        var lc = Create();
        var id = lc.Init(SampleConfig());

        lc.GetState(id).Phase.Should().Be(ContestPhase.Initialized);
        lc.Start(id);
        lc.GetState(id).Phase.Should().Be(ContestPhase.Running);
        lc.Pause(id);
        lc.GetState(id).Phase.Should().Be(ContestPhase.Paused);
        lc.Resume(id);
        lc.GetState(id).Phase.Should().Be(ContestPhase.Running);
        lc.End(id);
        lc.GetState(id).Phase.Should().Be(ContestPhase.Ended);

        var log = lc.GetPhaseLog(id);
        log.Should().HaveCount(5);
        log[0].ToPhase.Should().Be(ContestPhase.Initialized);
        log[4].ToPhase.Should().Be(ContestPhase.Ended);
    }

    [Fact]
    public void Init_BlankName_Throws()
    {
        var lc = Create();
        var act = () => lc.Init(SampleConfig() with { Name = "   " });
        act.Should().Throw<ArgumentException>().WithMessage("*name*");
    }

    [Fact]
    public void Init_EmptyPersonas_Throws()
    {
        var lc = Create();
        var act = () => lc.Init(SampleConfig() with { PersonaIds = Array.Empty<string>() });
        act.Should().Throw<ArgumentException>().WithMessage("*persona*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Init_NonPositiveDuration_Throws(double hours)
    {
        var lc = Create();
        var act = () => lc.Init(SampleConfig(duration: hours));
        act.Should().Throw<ArgumentException>().WithMessage("*DurationHours*");
    }

    [Fact]
    public void Pause_FromNonRunning_Throws()
    {
        var lc = Create();
        var id = lc.Init(SampleConfig());
        var act = () => lc.Pause(id);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Pause*");
    }

    [Fact]
    public void End_BeforeStart_Allowed()
    {
        var lc = Create();
        var id = lc.Init(SampleConfig());
        lc.End(id);
        lc.GetState(id).Phase.Should().Be(ContestPhase.Ended);
    }

    [Fact]
    public void End_Twice_Throws()
    {
        var lc = Create();
        var id = lc.Init(SampleConfig());
        lc.End(id);
        var act = () => lc.End(id);
        act.Should().Throw<InvalidOperationException>().WithMessage("*already*");
    }

    [Fact]
    public void PauseResume_PreservesLeaderboard()
    {
        var lc = Create();
        var id = lc.Init(SampleConfig());
        lc.Start(id);
        lc.RecordScore(id, "moss", 10);
        lc.RecordScore(id, "roma", 5);

        var before = lc.GetLeaderboard(id);
        lc.Pause(id);
        lc.Resume(id);
        var after = lc.GetLeaderboard(id);

        after.Should().Equal(before);
        after[0].PersonaId.Should().Be("moss");
        after[0].Score.Should().Be(10);
    }

    [Fact]
    public void Leaderboard_OrderedByScoreDescending_TieBreakByPersonaIdOrdinal()
    {
        var lc = Create();
        var id = lc.Init(SampleConfig());
        lc.Start(id);
        lc.RecordScore(id, "moss", 5);
        lc.RecordScore(id, "roma", 5);
        lc.RecordScore(id, "levene", 1);

        var lb = lc.GetLeaderboard(id);
        lb[0].PersonaId.Should().Be("moss");   // tie: ordinal sort moss vs roma, moss < roma
        lb[1].PersonaId.Should().Be("roma");
        lb[2].PersonaId.Should().Be("levene");
    }

    [Fact]
    public void RecordScore_OnInactivePersona_Throws()
    {
        var lc = Create();
        var id = lc.Init(SampleConfig());
        lc.Start(id);
        var act = () => lc.RecordScore(id, "williamson", 5);
        act.Should().Throw<ArgumentException>().WithMessage("*active*");
    }
}
