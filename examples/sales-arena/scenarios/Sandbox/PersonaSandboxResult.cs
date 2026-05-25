namespace SalesArena.Sandbox;

public sealed record PersonaSandboxResult(
    string Persona,
    bool Completed,
    int ExecutedSteps,
    int TouchesUsed,
    int ToolCallsUsed,
    TimeSpan RuntimeUsed,
    IReadOnlyList<SandboxViolation> Violations);
