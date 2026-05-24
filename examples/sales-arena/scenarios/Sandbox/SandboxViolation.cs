namespace SalesArena.Sandbox;

public sealed record SandboxViolation(
    SandboxErrorCode Code,
    string Message,
    int StepIndex,
    int TouchesUsed,
    int ToolCallsUsed,
    TimeSpan RuntimeUsed);
