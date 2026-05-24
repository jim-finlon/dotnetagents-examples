namespace SalesArena.Sandbox;

/// <summary>
/// One requested action from a persona script. The sandbox uses the declared
/// cost fields to enforce limits before a real tool adapter is wired in.
/// </summary>
public sealed record SandboxStep(
    SandboxStepKind Kind,
    string? Target = null,
    string? ToolName = null,
    TimeSpan? RuntimeCost = null)
{
    public TimeSpan EffectiveRuntimeCost => RuntimeCost ?? TimeSpan.Zero;
}
