namespace SalesArena.Sandbox;

/// <summary>
/// Hard caps for one sandboxed persona run.
/// </summary>
public sealed record SandboxLimits
{
    /// <summary>Maximum outbound touches a persona may attempt in one run.</summary>
    public int MaxTouchesPerRun { get; init; } = 20;

    /// <summary>Maximum tool calls a persona may request in one run.</summary>
    public int MaxToolCallsPerRun { get; init; } = 10;

    /// <summary>Maximum runtime budget for one run.</summary>
    public TimeSpan MaxRuntime { get; init; } = TimeSpan.FromSeconds(5);
}
