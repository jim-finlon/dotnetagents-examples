using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace SalesArena.Ops.Tests;

public class OpsAgentBuildDailyQueueTests
{
    private static OpsAgent Create() => new();

    [Fact]
    public void BuildDailyQueue_HappyPath_StableOrdered()
    {
        var agent = Create();

        var queue = agent.BuildDailyQueue(new BuildQueueRequest(
            PersonaId: "moss",
            LeadHandles: new[] { "greybridge", "stratham", "northwood" },
            OptCap: 0));

        queue.PersonaId.Should().Be("moss");
        queue.Items.Should().HaveCount(3);
        queue.Items.Select(i => i.LeadHandle)
            .Should().Equal(new[] { "greybridge", "northwood", "stratham" });
        queue.Items.Select(i => i.Position).Should().Equal(new[] { 1, 2, 3 });
    }

    [Fact]
    public void BuildDailyQueue_SameInput_StableOutput()
    {
        var agent = Create();
        var input = new BuildQueueRequest(
            PersonaId: "moss",
            LeadHandles: new[] { "northwood", "greybridge", "stratham" },
            OptCap: 0);

        var first = agent.BuildDailyQueue(input);
        var second = agent.BuildDailyQueue(input);

        first.Items.Should().Equal(second.Items);
    }

    [Fact]
    public void BuildDailyQueue_OptCap_RespectsLimit()
    {
        var agent = Create();

        var queue = agent.BuildDailyQueue(new BuildQueueRequest(
            PersonaId: "moss",
            LeadHandles: new[] { "alpha", "bravo", "charlie", "delta", "echo" },
            OptCap: 2));

        queue.Items.Should().HaveCount(2);
        queue.Items.Select(i => i.LeadHandle).Should().Equal(new[] { "alpha", "bravo" });
    }

    [Fact]
    public void BuildDailyQueue_Deduplicates()
    {
        var agent = Create();

        var queue = agent.BuildDailyQueue(new BuildQueueRequest(
            PersonaId: "moss",
            LeadHandles: new[] { "greybridge", "greybridge", "stratham" },
            OptCap: 0));

        queue.Items.Should().HaveCount(2);
    }

    [Fact]
    public void BuildDailyQueue_BlankPersonaId_Rejected()
    {
        var agent = Create();

        var act = () => agent.BuildDailyQueue(new BuildQueueRequest(
            PersonaId: "   ",
            LeadHandles: new[] { "greybridge" },
            OptCap: 0));

        act.Should().Throw<ArgumentException>().WithMessage("*PersonaId*");
    }

    [Fact]
    public void BuildDailyQueue_NegativeOptCap_Rejected()
    {
        var agent = Create();

        var act = () => agent.BuildDailyQueue(new BuildQueueRequest(
            PersonaId: "moss",
            LeadHandles: new[] { "greybridge" },
            OptCap: -1));

        act.Should().Throw<ArgumentException>().WithMessage("*OptCap*");
    }

    [Fact]
    public void BuildDailyQueue_FiltersBlankLeads()
    {
        var agent = Create();

        var queue = agent.BuildDailyQueue(new BuildQueueRequest(
            PersonaId: "moss",
            LeadHandles: new[] { "greybridge", "  ", "" },
            OptCap: 0));

        queue.Items.Should().HaveCount(1);
        queue.Items[0].LeadHandle.Should().Be("greybridge");
    }
}
