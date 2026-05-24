using FluentAssertions;
using SalesArena.Sandbox;
using Xunit;

namespace SalesArena.Sandbox.Tests;

public sealed class PersonaSandboxHostTests
{
    [Fact]
    public void Run_completes_when_steps_stay_inside_all_limits()
    {
        var host = new PersonaSandboxHost(new SandboxLimits
        {
            MaxTouchesPerRun = 3,
            MaxToolCallsPerRun = 2,
            MaxRuntime = TimeSpan.FromSeconds(3),
        });

        var result = host.Run(new PersonaSandboxRequest("roma", new[]
        {
            new SandboxStep(SandboxStepKind.Think, RuntimeCost: TimeSpan.FromMilliseconds(250)),
            new SandboxStep(SandboxStepKind.Touch, Target: "lead-1", RuntimeCost: TimeSpan.FromMilliseconds(500)),
            new SandboxStep(SandboxStepKind.ToolCall, ToolName: "crm.lookup", RuntimeCost: TimeSpan.FromMilliseconds(750)),
            new SandboxStep(SandboxStepKind.Touch, Target: "lead-2", RuntimeCost: TimeSpan.FromMilliseconds(500)),
        }));

        result.Completed.Should().BeTrue();
        result.ExecutedSteps.Should().Be(4);
        result.TouchesUsed.Should().Be(2);
        result.ToolCallsUsed.Should().Be(1);
        result.RuntimeUsed.Should().Be(TimeSpan.FromSeconds(2));
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Run_stops_before_executing_touch_that_exceeds_volume_budget()
    {
        var host = new PersonaSandboxHost(new SandboxLimits
        {
            MaxTouchesPerRun = 2,
            MaxToolCallsPerRun = 5,
            MaxRuntime = TimeSpan.FromSeconds(10),
        });

        var result = host.Run(new PersonaSandboxRequest("moss", new[]
        {
            new SandboxStep(SandboxStepKind.Touch, Target: "lead-1"),
            new SandboxStep(SandboxStepKind.Touch, Target: "lead-2"),
            new SandboxStep(SandboxStepKind.Touch, Target: "lead-3"),
        }));

        result.Completed.Should().BeFalse();
        result.ExecutedSteps.Should().Be(2);
        result.TouchesUsed.Should().Be(2);
        result.Violations.Should().ContainSingle();
        result.Violations[0].Code.Should().Be(SandboxErrorCode.TouchBudgetExceeded);
        result.Violations[0].StepIndex.Should().Be(2);
    }

    [Fact]
    public void Run_stops_before_executing_tool_call_that_exceeds_budget()
    {
        var host = new PersonaSandboxHost(new SandboxLimits
        {
            MaxTouchesPerRun = 5,
            MaxToolCallsPerRun = 1,
            MaxRuntime = TimeSpan.FromSeconds(10),
        });

        var result = host.Run(new PersonaSandboxRequest("levene", new[]
        {
            new SandboxStep(SandboxStepKind.ToolCall, ToolName: "crm.lookup"),
            new SandboxStep(SandboxStepKind.ToolCall, ToolName: "email.send"),
        }));

        result.Completed.Should().BeFalse();
        result.ExecutedSteps.Should().Be(1);
        result.ToolCallsUsed.Should().Be(1);
        result.Violations.Should().ContainSingle(v => v.Code == SandboxErrorCode.ToolBudgetExceeded);
    }

    [Fact]
    public void Run_stops_before_step_that_exceeds_runtime_limit()
    {
        var host = new PersonaSandboxHost(new SandboxLimits
        {
            MaxTouchesPerRun = 5,
            MaxToolCallsPerRun = 5,
            MaxRuntime = TimeSpan.FromSeconds(2),
        });

        var result = host.Run(new PersonaSandboxRequest("aaronow", new[]
        {
            new SandboxStep(SandboxStepKind.Think, RuntimeCost: TimeSpan.FromSeconds(1)),
            new SandboxStep(SandboxStepKind.ToolCall, ToolName: "crm.lookup", RuntimeCost: TimeSpan.FromMilliseconds(900)),
            new SandboxStep(SandboxStepKind.Touch, Target: "lead-1", RuntimeCost: TimeSpan.FromMilliseconds(200)),
        }));

        result.Completed.Should().BeFalse();
        result.ExecutedSteps.Should().Be(2);
        result.RuntimeUsed.Should().Be(TimeSpan.FromMilliseconds(1900));
        result.Violations.Should().ContainSingle(v => v.Code == SandboxErrorCode.RuntimeLimitExceeded);
    }

    [Fact]
    public void Constructor_rejects_non_positive_limits()
    {
        Action zeroTouches = () => _ = new PersonaSandboxHost(new SandboxLimits { MaxTouchesPerRun = 0 });
        Action zeroTools = () => _ = new PersonaSandboxHost(new SandboxLimits { MaxToolCallsPerRun = 0 });
        Action zeroRuntime = () => _ = new PersonaSandboxHost(new SandboxLimits { MaxRuntime = TimeSpan.Zero });

        zeroTouches.Should().Throw<ArgumentOutOfRangeException>();
        zeroTools.Should().Throw<ArgumentOutOfRangeException>();
        zeroRuntime.Should().Throw<ArgumentOutOfRangeException>();
    }
}
