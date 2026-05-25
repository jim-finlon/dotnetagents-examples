using System.Text.Json;
using System.Text.Json.Serialization;

using DotNetAgents.Core.PublicExamples;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => await RunSmokeAsync().ConfigureAwait(false),
    "writer-editor-judge" => await WriteJsonAsync(OrchestrationExamples.RunWriterEditorJudge()),
    "planner-executor-verifier" => await WriteJsonAsync(OrchestrationExamples.RunPlannerExecutorVerifier()),
    "approval" => await WriteJsonAsync(OrchestrationExamples.RunPreviewConfirmApproval()),
    "compare" => await WriteJsonAsync(OrchestrationExamples.ComparePatterns()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static async Task<int> RunSmokeAsync()
{
    var results = new OrchestrationExampleResult[]
    {
        OrchestrationExamples.RunWriterEditorJudge(),
        OrchestrationExamples.RunPlannerExecutorVerifier(),
        OrchestrationExamples.RunPreviewConfirmApproval(),
        OrchestrationExamples.ComparePatterns()
    };

    var passed = results.Length == 4 &&
                 results.All(result => result.Status == "passed") &&
                 results.All(result => result.ResultEnvelope.SchemaVersion == PublicExampleResultEnvelopeContract.SchemaVersion) &&
                 results.All(result => result.ResultEnvelope.LocalValidation.IsPassed);

    return await WriteJsonAsync(new OrchestrationSmokeResult(
        passed ? "passed" : "failed",
        "orchestration",
        results.Length,
        results,
        results.Select(result => result.ResultEnvelope).ToArray()), passed ? 0 : 1).ConfigureAwait(false);
}

static int WriteHelp()
{
    Console.WriteLine("Orchestration examples pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/orchestration -- --smoke");
    Console.WriteLine("  dotnet run --project examples/orchestration -- writer-editor-judge");
    Console.WriteLine("  dotnet run --project examples/orchestration -- planner-executor-verifier");
    Console.WriteLine("  dotnet run --project examples/orchestration -- approval");
    Console.WriteLine("  dotnet run --project examples/orchestration -- compare");
    Console.WriteLine();
    Console.WriteLine("All commands run offline with deterministic public-safe roles and synthetic data.");
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

internal static class OrchestrationExamples
{
    public static OrchestrationExampleResult RunWriterEditorJudge()
    {
        var request = new DraftRequest(
            Topic: "Explain why typed result envelopes matter for public agent examples.",
            Audience: "C# application developers",
            Constraints: ["concise", "public-safe", "actionable"]);

        var writer = new WriterAgent();
        var editor = new EditorAgent();
        var judge = new JudgeAgent();

        var draft = writer.Write(request);
        var revision = editor.Edit(draft);
        var verdict = judge.Judge(revision);

        var passed = draft.Sections.Count == 3 &&
                     revision.Edits.Any(edit => edit.Contains("Added validation note", StringComparison.Ordinal)) &&
                     verdict.Approved &&
                     verdict.Score >= 0.90m;

        return CreateResult(
            id: "orchestration-writer-editor-judge",
            title: "Writer Editor Judge",
            capability: "Separate generation, critique, revision, and judging into typed deterministic roles.",
            checks:
            [
                "writer produced a structured draft",
                "editor returned explicit edits",
                "judge returned a typed verdict",
                "approved output met score threshold"
            ],
            artifacts: ["writer-editor-judge-transcript.json"],
            details: new
            {
                request,
                draft,
                revision,
                verdict
            },
            metrics: new Dictionary<string, decimal>
            {
                ["roles"] = 3,
                ["edits"] = revision.Edits.Count,
                ["judgeScore"] = verdict.Score
            },
            passed);
    }

    public static OrchestrationExampleResult RunPlannerExecutorVerifier()
    {
        var goal = new DeliveryGoal(
            GoalId: "goal-public-001",
            Summary: "Publish a local public example with build, smoke, and docs evidence.",
            RequiredEvidence: ["build", "smoke", "docs"]);

        var planner = new PlannerAgent();
        var executor = new ExecutorAgent();
        var verifier = new VerifierAgent();

        var plan = planner.Plan(goal);
        var execution = executor.Execute(plan);
        var verification = verifier.Verify(goal, plan, execution);

        var passed = plan.Steps.Count == 3 &&
                     execution.StepResults.All(result => result.Status == "completed") &&
                     verification.Passed &&
                     verification.MissingEvidence.Count == 0;

        return CreateResult(
            id: "orchestration-planner-executor-verifier",
            title: "Planner Executor Verifier",
            capability: "Split planning, execution, and verification so the checker is not the worker.",
            checks:
            [
                "planner emitted ordered steps",
                "executor produced one result per step",
                "verifier checked required evidence",
                "verification output remained structured"
            ],
            artifacts: ["planner-executor-verifier-transcript.json"],
            details: new
            {
                goal,
                plan,
                execution,
                verification
            },
            metrics: new Dictionary<string, decimal>
            {
                ["plannedSteps"] = plan.Steps.Count,
                ["executedSteps"] = execution.StepResults.Count,
                ["missingEvidence"] = verification.MissingEvidence.Count
            },
            passed);
    }

    public static OrchestrationExampleResult RunPreviewConfirmApproval()
    {
        var action = new ProposedAction(
            ActionId: "action-public-001",
            Description: "Publish a synthetic support reply to a local transcript file.",
            Risk: "external-write",
            PreviewText: "Would write support-reply.md with a polite explanation and next step.");

        var approval = new PreviewConfirmApproval();
        var preview = approval.Preview(action);
        var refused = approval.Confirm(preview.PreviewId, confirmationToken: "wrong-token");
        var confirmed = approval.Confirm(preview.PreviewId, preview.ConfirmationToken);

        var passed = preview.RequiresConfirmation &&
                     refused.Status == "refused" &&
                     confirmed.Status == "confirmed" &&
                     confirmed.EvidenceRef == "approval/action-public-001.json";

        return CreateResult(
            id: "orchestration-preview-confirm-approval",
            title: "Human In The Loop Approval",
            capability: "Preview a synthetic external action, refuse an invalid confirmation, then confirm with the matching token.",
            checks:
            [
                "preview did not mutate external state",
                "invalid confirmation was refused",
                "valid confirmation emitted evidence ref",
                "action used synthetic local data"
            ],
            artifacts: ["preview-confirm-approval-transcript.json"],
            details: new
            {
                action,
                preview = preview with { ConfirmationToken = "<redacted-demo-token>" },
                refused,
                confirmed
            },
            metrics: new Dictionary<string, decimal>
            {
                ["previews"] = 1,
                ["refusals"] = 1,
                ["confirmations"] = 1
            },
            passed);
    }

    public static OrchestrationExampleResult ComparePatterns()
    {
        var patterns = new[]
        {
            new OrchestrationPattern("writer-editor-judge", "Use when output quality improves through critique and an independent verdict.", "draft public docs or examples"),
            new OrchestrationPattern("planner-executor-verifier", "Use when execution should be checked against requirements by a separate role.", "ship a local example with evidence"),
            new OrchestrationPattern("preview-confirm-approval", "Use when a proposed action should be visible before it is committed.", "review a synthetic external write")
        };

        return CreateResult(
            id: "orchestration-pattern-comparison",
            title: "Orchestration Pattern Comparison",
            capability: "Choose a multi-role orchestration shape by risk and evidence needs.",
            checks:
            [
                "critique loop guidance present",
                "verification gate guidance present",
                "approval checkpoint guidance present"
            ],
            artifacts: ["orchestration-pattern-comparison.json"],
            details: patterns,
            metrics: new Dictionary<string, decimal>
            {
                ["patterns"] = patterns.Length
            },
            passed: patterns.Length == 3);
    }

    private static OrchestrationExampleResult CreateResult(
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

        return new OrchestrationExampleResult(
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

internal sealed class WriterAgent
{
    public DraftDocument Write(DraftRequest request)
    {
        return new DraftDocument(
            Request: request,
            Sections:
            [
                new DraftSection("problem", "Agent examples need outputs that applications can verify."),
                new DraftSection("approach", "Wrap each run in a stable result envelope with checks, artifacts, and metrics."),
                new DraftSection("result", "Developers can build, smoke, compare, and extend the example safely.")
            ]);
    }
}

internal sealed class EditorAgent
{
    public EditedDocument Edit(DraftDocument draft)
    {
        var revisedSections = draft.Sections
            .Select(section => section.Title == "approach"
                ? section with { Text = section.Text + " Keep the envelope deterministic in smoke mode." }
                : section)
            .ToArray();

        return new EditedDocument(
            Original: draft,
            RevisedSections: revisedSections,
            Edits:
            [
                "Added validation note to approach section.",
                "Confirmed each section uses public-safe phrasing."
            ]);
    }
}

internal sealed class JudgeAgent
{
    public JudgeVerdict Judge(EditedDocument document)
    {
        var hasProblem = document.RevisedSections.Any(section => section.Title == "problem");
        var hasValidation = document.RevisedSections.Any(section => section.Text.Contains("deterministic", StringComparison.OrdinalIgnoreCase));
        var hasResult = document.RevisedSections.Any(section => section.Title == "result");
        var score = new[] { hasProblem, hasValidation, hasResult }.Count(item => item) / 3.0m;

        return new JudgeVerdict(
            Approved: score >= 0.90m,
            Score: score,
            Reasons:
            [
                hasProblem ? "states the developer problem" : "missing problem",
                hasValidation ? "names deterministic validation" : "missing validation",
                hasResult ? "states usable result" : "missing result"
            ]);
    }
}

internal sealed class PlannerAgent
{
    public ExecutionPlan Plan(DeliveryGoal goal)
    {
        return new ExecutionPlan(
            Goal: goal,
            Steps:
            [
                new ExecutionStep("build", "Compile the example project.", "build"),
                new ExecutionStep("smoke", "Run deterministic smoke mode.", "smoke"),
                new ExecutionStep("docs", "Update README and route docs.", "docs")
            ]);
    }
}

internal sealed class ExecutorAgent
{
    public ExecutionRun Execute(ExecutionPlan plan)
    {
        var results = plan.Steps
            .Select(step => new StepResult(step.Id, "completed", $"{step.EvidenceKind}:{step.Id}:ok"))
            .ToArray();

        return new ExecutionRun(plan.Goal.GoalId, results);
    }
}

internal sealed class VerifierAgent
{
    public VerificationReport Verify(DeliveryGoal goal, ExecutionPlan plan, ExecutionRun run)
    {
        var producedEvidence = run.StepResults.Select(result => result.EvidenceRef.Split(':')[0]).ToHashSet(StringComparer.Ordinal);
        var missing = goal.RequiredEvidence.Where(required => !producedEvidence.Contains(required)).ToArray();
        var allStepsExecuted = plan.Steps.All(step => run.StepResults.Any(result => result.StepId == step.Id && result.Status == "completed"));

        return new VerificationReport(
            Passed: missing.Length == 0 && allStepsExecuted,
            MissingEvidence: missing,
            CheckedSteps: plan.Steps.Select(step => step.Id).ToArray(),
            Notes: allStepsExecuted ? ["all planned steps completed"] : ["one or more planned steps did not complete"]);
    }
}

internal sealed class PreviewConfirmApproval
{
    private readonly Dictionary<string, ApprovalPreview> _previews = new(StringComparer.Ordinal);

    public ApprovalPreview Preview(ProposedAction action)
    {
        var token = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(action.ActionId))).ToLowerInvariant()[..16];
        var preview = new ApprovalPreview(
            PreviewId: $"preview-{action.ActionId}",
            Action: action,
            RequiresConfirmation: true,
            ConfirmationToken: token,
            Warnings: ["external write is synthetic in this example", "confirmation token must match preview"]);
        _previews[preview.PreviewId] = preview;
        return preview;
    }

    public ApprovalDecision Confirm(string previewId, string confirmationToken)
    {
        if (!_previews.TryGetValue(previewId, out var preview))
        {
            return new ApprovalDecision("refused", previewId, "preview not found", null);
        }

        if (!string.Equals(preview.ConfirmationToken, confirmationToken, StringComparison.Ordinal))
        {
            return new ApprovalDecision("refused", previewId, "confirmation token did not match preview", null);
        }

        return new ApprovalDecision("confirmed", previewId, "synthetic action approved", $"approval/{preview.Action.ActionId}.json");
    }
}

internal sealed record DraftRequest(string Topic, string Audience, IReadOnlyList<string> Constraints);

internal sealed record DraftDocument(DraftRequest Request, IReadOnlyList<DraftSection> Sections);

internal sealed record DraftSection(string Title, string Text);

internal sealed record EditedDocument(DraftDocument Original, IReadOnlyList<DraftSection> RevisedSections, IReadOnlyList<string> Edits);

internal sealed record JudgeVerdict(bool Approved, decimal Score, IReadOnlyList<string> Reasons);

internal sealed record DeliveryGoal(string GoalId, string Summary, IReadOnlyList<string> RequiredEvidence);

internal sealed record ExecutionPlan(DeliveryGoal Goal, IReadOnlyList<ExecutionStep> Steps);

internal sealed record ExecutionStep(string Id, string Description, string EvidenceKind);

internal sealed record ExecutionRun(string GoalId, IReadOnlyList<StepResult> StepResults);

internal sealed record StepResult(string StepId, string Status, string EvidenceRef);

internal sealed record VerificationReport(bool Passed, IReadOnlyList<string> MissingEvidence, IReadOnlyList<string> CheckedSteps, IReadOnlyList<string> Notes);

internal sealed record ProposedAction(string ActionId, string Description, string Risk, string PreviewText);

internal sealed record ApprovalPreview(
    string PreviewId,
    ProposedAction Action,
    bool RequiresConfirmation,
    string ConfirmationToken,
    IReadOnlyList<string> Warnings);

internal sealed record ApprovalDecision(string Status, string PreviewId, string Reason, string? EvidenceRef);

internal sealed record OrchestrationPattern(string PatternId, string UseWhen, string Example);

internal sealed record OrchestrationSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<OrchestrationExampleResult> Examples,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);

internal sealed record OrchestrationExampleResult(
    string Status,
    string ExampleId,
    string Title,
    string Capability,
    IReadOnlyList<string> Checks,
    object Details,
    IReadOnlyDictionary<string, decimal> Metrics,
    PublicExampleResultEnvelope ResultEnvelope);
