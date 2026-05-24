namespace SalesArena.Sandbox;

/// <summary>
/// Deterministic v1 sandbox host. It enforces touch, tool, and runtime budgets
/// against a proposed action list before any live tool adapters are invoked.
/// </summary>
public sealed class PersonaSandboxHost : IPersonaSandboxHost
{
    private readonly SandboxLimits _limits;

    public PersonaSandboxHost(SandboxLimits? limits = null)
    {
        _limits = limits ?? new SandboxLimits();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_limits.MaxTouchesPerRun);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_limits.MaxToolCallsPerRun);
        if (_limits.MaxRuntime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(limits), "MaxRuntime must be positive.");
        }
    }

    public PersonaSandboxResult Run(PersonaSandboxRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Persona);
        ArgumentNullException.ThrowIfNull(request.Steps);

        var touches = 0;
        var tools = 0;
        var runtime = TimeSpan.Zero;
        var executed = 0;
        var violations = new List<SandboxViolation>();

        for (var i = 0; i < request.Steps.Count; i++)
        {
            var step = request.Steps[i];
            var nextTouches = touches + (step.Kind == SandboxStepKind.Touch ? 1 : 0);
            var nextTools = tools + (step.Kind == SandboxStepKind.ToolCall ? 1 : 0);
            var nextRuntime = runtime + step.EffectiveRuntimeCost;

            if (nextTouches > _limits.MaxTouchesPerRun)
            {
                violations.Add(new SandboxViolation(
                    SandboxErrorCode.TouchBudgetExceeded,
                    $"touch budget exceeded: {nextTouches}/{_limits.MaxTouchesPerRun}",
                    i,
                    touches,
                    tools,
                    runtime));
                break;
            }

            if (nextTools > _limits.MaxToolCallsPerRun)
            {
                violations.Add(new SandboxViolation(
                    SandboxErrorCode.ToolBudgetExceeded,
                    $"tool budget exceeded: {nextTools}/{_limits.MaxToolCallsPerRun}",
                    i,
                    touches,
                    tools,
                    runtime));
                break;
            }

            if (nextRuntime > _limits.MaxRuntime)
            {
                violations.Add(new SandboxViolation(
                    SandboxErrorCode.RuntimeLimitExceeded,
                    $"runtime limit exceeded: {nextRuntime}/{_limits.MaxRuntime}",
                    i,
                    touches,
                    tools,
                    runtime));
                break;
            }

            touches = nextTouches;
            tools = nextTools;
            runtime = nextRuntime;
            executed++;
        }

        return new PersonaSandboxResult(
            request.Persona,
            Completed: violations.Count == 0,
            ExecutedSteps: executed,
            TouchesUsed: touches,
            ToolCallsUsed: tools,
            RuntimeUsed: runtime,
            Violations: violations);
    }
}
