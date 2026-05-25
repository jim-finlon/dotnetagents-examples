using System.Text.Json;
using System.Text.Json.Serialization;

using DotNetAgents.Agents.BehaviorTrees;
using DotNetAgents.Agents.StateMachines;
using DotNetAgents.Core.PublicExamples;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => await RunSmokeAsync().ConfigureAwait(false),
    "workflow" => await WriteJsonAsync(ControlLoopExamples.RunDurableWorkflow()),
    "state-machine" => await WriteJsonAsync(await ControlLoopExamples.RunStateMachineAsync().ConfigureAwait(false)),
    "behavior-tree" => await WriteJsonAsync(await ControlLoopExamples.RunBehaviorTreeAsync().ConfigureAwait(false)),
    "compare" => await WriteJsonAsync(ControlLoopExamples.ComparePatterns()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static async Task<int> RunSmokeAsync()
{
    var results = new ControlLoopExampleResult[]
    {
        ControlLoopExamples.RunDurableWorkflow(),
        await ControlLoopExamples.RunStateMachineAsync().ConfigureAwait(false),
        await ControlLoopExamples.RunBehaviorTreeAsync().ConfigureAwait(false),
        ControlLoopExamples.ComparePatterns()
    };

    var passed = results.Length == 4 &&
                 results.All(result => result.Status == "passed") &&
                 results.All(result => result.ResultEnvelope.SchemaVersion == PublicExampleResultEnvelopeContract.SchemaVersion) &&
                 results.All(result => result.ResultEnvelope.LocalValidation.IsPassed);

    return await WriteJsonAsync(new ControlLoopSmokeResult(
        passed ? "passed" : "failed",
        "control-loops",
        results.Length,
        results,
        results.Select(result => result.ResultEnvelope).ToArray()), passed ? 0 : 1).ConfigureAwait(false);
}

static int WriteHelp()
{
    Console.WriteLine("Control-loop examples pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/control-loops -- --smoke");
    Console.WriteLine("  dotnet run --project examples/control-loops -- workflow");
    Console.WriteLine("  dotnet run --project examples/control-loops -- state-machine");
    Console.WriteLine("  dotnet run --project examples/control-loops -- behavior-tree");
    Console.WriteLine("  dotnet run --project examples/control-loops -- compare");
    Console.WriteLine();
    Console.WriteLine("All commands run offline with synthetic local state.");
    return 0;
}

static int WriteError(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

static async Task<int> WriteJsonAsync<T>(T value, int exitCode = 0)
{
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(value, jsonOptions)).ConfigureAwait(false);
    return exitCode;
}

internal static class ControlLoopExamples
{
    public static ControlLoopExampleResult RunDurableWorkflow()
    {
        var workflow = DurableWorkflowSimulator.CreateSupportWorkflow();
        var started = workflow.RunUntilPause(new SupportCaseWorkflowState(
            CaseId: "case-public-001",
            CustomerTier: "standard",
            SignalQuality: 0.74m,
            HumanApprovalGranted: false));

        var resumed = workflow.ResumeFromCheckpoint(started.Checkpoint with
        {
            State = started.State with { HumanApprovalGranted = true }
        });

        var passed = started.Checkpoint.NextStepId == "human-approval" &&
                     resumed.Status == "completed" &&
                     resumed.State.CompletedStepIds.SequenceEqual(["intake", "classify", "draft", "human-approval", "publish"]);

        return CreateResult(
            id: "control-loop-durable-workflow",
            title: "Durable Workflow",
            capability: "Run a deterministic workflow until an approval checkpoint, then resume from that checkpoint.",
            checks:
            [
                "workflow paused at human approval",
                "checkpoint captured next step id",
                "resume completed remaining steps",
                "trace preserved completed step order"
            ],
            artifacts: ["durable-workflow-transcript.json"],
            details: new
            {
                startedStatus = started.Status,
                checkpointNextStepId = started.Checkpoint.NextStepId,
                startedCompletedStepIds = started.State.CompletedStepIds,
                resumedStatus = resumed.Status,
                resumedCompletedStepIds = resumed.State.CompletedStepIds,
                resumedOutputs = resumed.State.Outputs
            },
            metrics: new Dictionary<string, decimal>
            {
                ["checkpointCount"] = 1,
                ["completedSteps"] = resumed.State.CompletedStepIds.Count,
                ["networkCalls"] = 0
            },
            passed);
    }

    public static async Task<ControlLoopExampleResult> RunStateMachineAsync()
    {
        var ticket = new SupportTicketState("ticket-public-001");
        var stateMachine = new AgentStateMachine<SupportTicketState>();
        stateMachine.AddState("New", entryAction: state => state.Record("entered:new"));
        stateMachine.AddState("Triaged", entryAction: state => state.Record("entered:triaged"));
        stateMachine.AddState("WaitingForApproval", entryAction: state => state.Record("entered:waiting-for-approval"));
        stateMachine.AddState("Resolved", entryAction: state => state.Record("entered:resolved"));
        stateMachine.AddTransition("New", "Triaged", state => state.HasInput, state => state.Record("transition:new-to-triaged"));
        stateMachine.AddTransition("Triaged", "WaitingForApproval", state => state.RequiresApproval, state => state.Record("transition:approval-needed"));
        stateMachine.AddTransition("WaitingForApproval", "Resolved", state => state.Approved, state => state.Record("transition:approved"));
        stateMachine.SetInitialState("New");

        ticket.InputSummary = "Synthetic refund request with enough detail for local triage.";
        ticket.RequiresApproval = true;
        await stateMachine.TransitionAsync("Triaged", ticket).ConfigureAwait(false);
        await stateMachine.TransitionAsync("WaitingForApproval", ticket).ConfigureAwait(false);
        ticket.Approved = true;
        await stateMachine.TransitionAsync("Resolved", ticket).ConfigureAwait(false);

        var passed = stateMachine.CurrentState == "Resolved" &&
                     stateMachine.TransitionHistory.Count == 3 &&
                     ticket.Events.Contains("transition:approved", StringComparer.Ordinal);

        return CreateResult(
            id: "control-loop-state-machine",
            title: "State Machine",
            capability: "Use AgentStateMachine<T> to make lifecycle states, guards, and transitions explicit.",
            checks:
            [
                "initial state set",
                "guarded transition accepted when input exists",
                "approval guard blocked until approval flag was set",
                "transition history recorded"
            ],
            artifacts: ["state-machine-transcript.json"],
            details: new
            {
                currentState = stateMachine.CurrentState,
                transitionHistory = stateMachine.TransitionHistory.Select(item => new { item.FromState, item.ToState }),
                ticket.Events
            },
            metrics: new Dictionary<string, decimal>
            {
                ["states"] = 4,
                ["transitions"] = stateMachine.TransitionHistory.Count,
                ["events"] = ticket.Events.Count
            },
            passed);
    }

    public static async Task<ControlLoopExampleResult> RunBehaviorTreeAsync()
    {
        var context = new SupportPolicyContext(
            caseId: "case-public-002",
            confidence: 0.82m,
            isHighRisk: false,
            hasCustomerContext: true);

        var root = new SelectorNode<SupportPolicyContext>("support-policy");
        var directResolution = new SequenceNode<SupportPolicyContext>("direct-resolution")
            .AddChildren(
                new ConditionNode<SupportPolicyContext>("has-context", state => state.HasCustomerContext),
                new ConditionNode<SupportPolicyContext>("confidence-ok", state => state.Confidence >= 0.80m),
                new ConditionNode<SupportPolicyContext>("not-high-risk", state => !state.IsHighRisk),
                new ActionNode<SupportPolicyContext>("draft-resolution", state =>
                {
                    state.Decision = "draft-resolution";
                    state.Trace.Add("drafted resolution from local policy");
                    return BehaviorTreeNodeStatus.Success;
                }));

        var requestMoreContext = new ActionNode<SupportPolicyContext>("request-more-context", state =>
        {
            state.Decision = "request-more-context";
            state.Trace.Add("requested additional context");
            return BehaviorTreeNodeStatus.Success;
        });

        root.AddChildren(directResolution, requestMoreContext);
        var tree = new BehaviorTree<SupportPolicyContext>("support-case-policy", root)
        {
            Description = "Public local policy selector for support-case next action."
        };

        var status = await tree.ExecuteAsync(context).ConfigureAwait(false);

        var passed = status == BehaviorTreeNodeStatus.Success &&
                     context.Decision == "draft-resolution" &&
                     context.Trace.Count == 1;

        return CreateResult(
            id: "control-loop-behavior-tree",
            title: "Behavior Tree",
            capability: "Use selector, sequence, condition, and action nodes for tactical decision flow.",
            checks:
            [
                "selector evaluated first viable branch",
                "conditions passed in sequence",
                "action node recorded next decision",
                "fallback remained available"
            ],
            artifacts: ["behavior-tree-transcript.json"],
            details: new
            {
                tree.Name,
                tree.Description,
                status = status.ToString(),
                context.Decision,
                context.Trace
            },
            metrics: new Dictionary<string, decimal>
            {
                ["branches"] = 2,
                ["traceEvents"] = context.Trace.Count,
                ["networkCalls"] = 0
            },
            passed);
    }

    public static ControlLoopExampleResult ComparePatterns()
    {
        var comparison = new[]
        {
            new ControlLoopPattern("durable-workflow", "Use when ordered work must pause, resume, retry, or produce step evidence.", "support case intake to approval to publish"),
            new ControlLoopPattern("state-machine", "Use when valid lifecycle states and transitions are the central safety rule.", "ticket state from New to Triaged to Resolved"),
            new ControlLoopPattern("behavior-tree", "Use when tactical policy should try preferred branches before fallbacks.", "choose draft, ask for context, or escalate")
        };

        return CreateResult(
            id: "control-loop-pattern-comparison",
            title: "Pattern Comparison",
            capability: "Choose a control-loop pattern by process shape instead of implementation fashion.",
            checks:
            [
                "workflow guidance present",
                "state-machine guidance present",
                "behavior-tree guidance present"
            ],
            artifacts: ["control-loop-pattern-comparison.json"],
            details: comparison,
            metrics: new Dictionary<string, decimal>
            {
                ["patterns"] = comparison.Length
            },
            passed: comparison.Length == 3);
    }

    private static ControlLoopExampleResult CreateResult(
        string id,
        string title,
        string capability,
        IReadOnlyList<string> checks,
        IReadOnlyList<string> artifacts,
        object details,
        IReadOnlyDictionary<string, decimal> metrics,
        bool passed)
    {
        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: id,
            exampleVersion: "1.0.0",
            inputSummary: capability,
            localValidation: new PublicExampleValidationSummary(passed ? "passed" : "failed", checks),
            outputArtifactRefs: artifacts.Select(artifact => new PublicExampleOutputArtifactRef("transcript", artifact, "application/json")),
            selfReportedMetrics: metrics,
            runId: $"{id}-offline-smoke");

        return new ControlLoopExampleResult(
            passed ? "passed" : "failed",
            id,
            title,
            capability,
            checks,
            details,
            metrics,
            envelope);
    }
}

internal sealed class DurableWorkflowSimulator
{
    private readonly IReadOnlyList<DurableWorkflowStep> _steps;

    private DurableWorkflowSimulator(IReadOnlyList<DurableWorkflowStep> steps)
    {
        _steps = steps;
    }

    public static DurableWorkflowSimulator CreateSupportWorkflow()
    {
        return new DurableWorkflowSimulator(
        [
            new DurableWorkflowStep("intake", state => state.RecordStep("intake", "case accepted")),
            new DurableWorkflowStep("classify", state => state.RecordStep("classify", state.SignalQuality >= 0.70m ? "ready for draft" : "needs context")),
            new DurableWorkflowStep("draft", state => state.RecordStep("draft", $"drafted response for {state.CustomerTier} customer")),
            new DurableWorkflowStep("human-approval", state => state.HumanApprovalGranted
                ? state.RecordStep("human-approval", "approval granted")
                : state.PauseAt("human-approval", "approval required before publish")),
            new DurableWorkflowStep("publish", state => state.RecordStep("publish", "published synthetic response"))
        ]);
    }

    public DurableWorkflowRun RunUntilPause(SupportCaseWorkflowState initialState)
    {
        return ContinueFrom(0, initialState);
    }

    public DurableWorkflowRun ResumeFromCheckpoint(DurableWorkflowCheckpoint checkpoint)
    {
        var nextIndex = _steps.ToList().FindIndex(step => step.Id == checkpoint.NextStepId);
        if (nextIndex < 0)
        {
            throw new InvalidOperationException($"Unknown checkpoint step '{checkpoint.NextStepId}'.");
        }

        return ContinueFrom(nextIndex, checkpoint.State);
    }

    private DurableWorkflowRun ContinueFrom(int startIndex, SupportCaseWorkflowState state)
    {
        var current = state;
        for (var index = startIndex; index < _steps.Count; index++)
        {
            var outcome = _steps[index].Run(current);
            current = outcome.State;
            if (outcome.Paused)
            {
                return new DurableWorkflowRun(
                    "paused",
                    current,
                    new DurableWorkflowCheckpoint(_steps[index].Id, outcome.Reason ?? "paused", current));
            }
        }

        return new DurableWorkflowRun("completed", current, new DurableWorkflowCheckpoint("completed", "workflow completed", current));
    }
}

internal sealed record DurableWorkflowStep(string Id, Func<SupportCaseWorkflowState, DurableWorkflowStepOutcome> Run);

internal sealed record DurableWorkflowStepOutcome(SupportCaseWorkflowState State, bool Paused, string? Reason = null);

internal sealed record DurableWorkflowCheckpoint(string NextStepId, string Reason, SupportCaseWorkflowState State);

internal sealed record DurableWorkflowRun(string Status, SupportCaseWorkflowState State, DurableWorkflowCheckpoint Checkpoint);

internal sealed record SupportCaseWorkflowState(
    string CaseId,
    string CustomerTier,
    decimal SignalQuality,
    bool HumanApprovalGranted,
    IReadOnlyList<string>? CompletedStepIds = null,
    IReadOnlyDictionary<string, string>? Outputs = null)
{
    public IReadOnlyList<string> CompletedStepIds { get; init; } = CompletedStepIds ?? [];

    public IReadOnlyDictionary<string, string> Outputs { get; init; } = Outputs ?? new Dictionary<string, string>();

    public DurableWorkflowStepOutcome RecordStep(string stepId, string output)
    {
        var steps = CompletedStepIds.Concat([stepId]).ToArray();
        var outputs = Outputs.ToDictionary(StringComparer.Ordinal);
        outputs[stepId] = output;
        return new DurableWorkflowStepOutcome(this with { CompletedStepIds = steps, Outputs = outputs }, Paused: false);
    }

    public DurableWorkflowStepOutcome PauseAt(string stepId, string reason)
    {
        return new DurableWorkflowStepOutcome(this, Paused: true, reason);
    }
}

internal sealed class SupportTicketState
{
    public SupportTicketState(string ticketId)
    {
        TicketId = ticketId;
    }

    public string TicketId { get; }

    public string InputSummary { get; set; } = string.Empty;

    public bool HasInput => !string.IsNullOrWhiteSpace(InputSummary);

    public bool RequiresApproval { get; set; }

    public bool Approved { get; set; }

    public List<string> Events { get; } = [];

    public void Record(string evt)
    {
        Events.Add(evt);
    }
}

internal sealed class SupportPolicyContext
{
    public SupportPolicyContext(string caseId, decimal confidence, bool isHighRisk, bool hasCustomerContext)
    {
        CaseId = caseId;
        Confidence = confidence;
        IsHighRisk = isHighRisk;
        HasCustomerContext = hasCustomerContext;
    }

    public string CaseId { get; }

    public decimal Confidence { get; }

    public bool IsHighRisk { get; }

    public bool HasCustomerContext { get; }

    public string Decision { get; set; } = "undecided";

    public List<string> Trace { get; } = [];
}

internal sealed record ControlLoopPattern(string PatternId, string UseWhen, string Example);

internal sealed record ControlLoopSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<ControlLoopExampleResult> Examples,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);

internal sealed record ControlLoopExampleResult(
    string Status,
    string ExampleId,
    string Title,
    string Capability,
    IReadOnlyList<string> Checks,
    object Details,
    IReadOnlyDictionary<string, decimal> Metrics,
    PublicExampleResultEnvelope ResultEnvelope);
