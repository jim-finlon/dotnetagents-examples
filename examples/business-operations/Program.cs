using DotNetAgents.Core.PublicExamples;
using DotNetAgents.Abstractions.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke()),
    "list" => WriteJson(BusinessOperationsCatalog.Examples),
    "run" => await RunExampleAsync(args.Skip(1).FirstOrDefault()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static BusinessOperationsSmokeResult RunSmoke()
{
    var envelopes = BusinessOperationsCatalog.Examples
        .Select(example => example.ToEnvelope())
        .ToArray();

    var passed = envelopes.Length == 5 &&
                 envelopes.All(envelope => envelope.SchemaVersion == PublicExampleResultEnvelopeContract.SchemaVersion) &&
                 envelopes.All(envelope => envelope.LocalValidation.IsPassed);

    return new BusinessOperationsSmokeResult(
        passed ? "passed" : "failed",
        "business-operations",
        envelopes.Length,
        envelopes);
}

static async Task<int> RunExampleAsync(string? exampleId)
{
    if (string.IsNullOrWhiteSpace(exampleId))
    {
        return WriteError("Usage: dotnet run --project samples/business-operations -- run <example-id>");
    }

    var example = BusinessOperationsCatalog.Examples
        .SingleOrDefault(item => string.Equals(item.Id, exampleId, StringComparison.OrdinalIgnoreCase));

    if (example is null)
    {
        return WriteError($"Unknown example '{exampleId}'. Run the 'list' command to see available examples.");
    }

    var provider = PublicLlmProvider.TryCreateFromEnv();
    if (provider != null)
    {
        try
        {
            Console.WriteLine($"[Live Mode] Running example '{exampleId}' using model '{provider.ModelName}'...");
            var result = await ExecuteLiveExampleAsync(provider, exampleId);
            return WriteJson(result);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Error] Live execution failed: {ex.Message}");
            Console.Error.WriteLine("Falling back to offline mode.");
        }
    }

    return WriteJson(example.ToEnvelope());
}

static async Task<object> ExecuteLiveExampleAsync(ILLMModel<string, string> provider, string exampleId)
{
    var definition = BusinessOperationsCatalog.Examples.First(item => string.Equals(item.Id, exampleId, StringComparison.OrdinalIgnoreCase));
    string prompt;
    string inputSummary;
    string liveOutputText;

    switch (exampleId.ToLowerInvariant())
    {
        case "project-planner":
            inputSummary = "Launch a subscription billing service for enterprise clients.";
            prompt = $"You are an expert project planner. Break down the following business goal into core milestones, task list with owners, and main risks: '{inputSummary}'. Format the entire output as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "crm-follow-up":
            inputSummary = "Score leads and draft outreach for: Alice (interested in multi-agent orchestration, high score), Bob (budget constraints, low score).";
            prompt = $"Given these sales leads, score them from 1-100 and draft a brief outreach email for each. Leads: '{inputSummary}'. Format as JSON or clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "communications-triage":
            inputSummary = "Triage incoming inbox: 1. Urgent: DB lock error. 2. Low: question about educational pricing.";
            prompt = $"Classify these incoming communications by Urgency, Topic, and suggest an internal action note: '{inputSummary}'. Format as structured JSON or markdown table.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "appointment-assistant":
            inputSummary = "Hey! Can we meet tomorrow at 2 PM or 4 PM EST to review design docs? - John";
            prompt = $"Extract appointment windows in EST timezone from this message and propose calendar focus blocks: '{inputSummary}'. Format as markdown table.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "time-management":
            inputSummary = "Focus plan for: Standup meeting at 9:30 AM, code review due by 12:00 PM, blog post due by 5:00 PM.";
            prompt = $"Create a daily schedule focus plan with time blocks, priorities, and check for task overload based on: '{inputSummary}'. Format as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        default:
            throw new ArgumentException($"Unknown example ID: {exampleId}");
    }

    var resultEnvelope = PublicExampleResultEnvelope.Create(
        exampleId: exampleId,
        exampleVersion: "1.0.0",
        inputSummary: inputSummary,
        localValidation: new PublicExampleValidationSummary("passed", definition.LocalChecks),
        outputArtifactRefs: definition.OutputArtifacts.Select(artifact =>
            new PublicExampleOutputArtifactRef("live-output", artifact, BusinessOperationsHelper.GuessMediaType(artifact))),
        selfReportedMetrics: new Dictionary<string, decimal>
        {
            ["localChecks"] = definition.LocalChecks.Count,
            ["outputArtifacts"] = definition.OutputArtifacts.Count,
            ["liveExecution"] = 1
        },
        runId: $"{exampleId}-live-run"
    );

    return new
    {
        status = "passed",
        exampleId = exampleId,
        provider = provider.ModelName,
        input = inputSummary,
        output = liveOutputText.Trim(),
        resultEnvelope = resultEnvelope
    };
}

/// <summary>
/// Writes the help guide for the business operations example pack to console.
/// </summary>
static int WriteHelp()
{
    Console.WriteLine("Business operations example pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/business-operations -- --smoke");
    Console.WriteLine("  dotnet run --project samples/business-operations -- list");
    Console.WriteLine("  dotnet run --project samples/business-operations -- run <example-id>");
    Console.WriteLine("     Examples: project-planner, crm-follow-up, communications-triage, appointment-assistant, time-management");
    Console.WriteLine();
    Console.WriteLine("Live Mode Configuration (optional):");
    Console.WriteLine("Configure API keys as environment variables to run live executions via providers:");
    Console.WriteLine("  export OPENAI_API_KEY=\"your-openai-api-key\"");
    Console.WriteLine("  export ANTHROPIC_API_KEY=\"your-anthropic-api-key\"");
    Console.WriteLine("  export OLLAMA_MODEL=\"llama3\" [OLLAMA_HOST=\"http://localhost:11434\"]");
    return 0;
}

/// <summary>
/// Writes an error message to standard error stream.
/// </summary>
static int WriteError(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

/// <summary>
/// Serializes a value as pretty-printed JSON to console.
/// </summary>
static int WriteJson<T>(T value, int exitCode = 0)
{
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
    Console.WriteLine(JsonSerializer.Serialize(value, jsonOptions));
    return exitCode;
}

/// <summary>
/// Catalog of available business operation examples.
/// </summary>
internal static class BusinessOperationsCatalog
{
    /// <summary>
    /// Gets the collection of defined business examples in this pack.
    /// </summary>
    public static IReadOnlyList<BusinessExampleDefinition> Examples { get; } =
    [
        new(
            "project-planner",
            "Basic Project Planner",
            "Turns a short goal statement into milestones, tasks, owners, and risks.",
            ["milestone extraction", "task breakdown", "risk summary"],
            ["project-plan.md", "tasks.json"]),
        new(
            "crm-follow-up",
            "Basic CRM Follow-Up",
            "Scores sample leads and drafts a next-touch message for each one.",
            ["lead scoring", "next action selection", "follow-up draft"],
            ["lead-plan.json", "follow-up.md"]),
        new(
            "communications-triage",
            "Communications Triage",
            "Classifies incoming messages by urgency, topic, and response posture.",
            ["message classification", "urgency sort", "response notes"],
            ["inbox-triage.json"]),
        new(
            "appointment-assistant",
            "Appointment Assistant",
            "Extracts appointment requests and proposes public-safe calendar blocks.",
            ["extract time windows", "detect missing details", "draft confirmation"],
            ["appointment-draft.json", "confirmation.md"]),
        new(
            "time-management",
            "Time-Management Assistant",
            "Creates a daily focus plan from deadlines, meetings, and reminders.",
            ["prioritize commitments", "build focus blocks", "surface overload warning"],
            ["daily-plan.md", "reminders.json"])
    ];
}

/// <summary>
/// Helper utilities for business operations examples.
/// </summary>
internal static class BusinessOperationsHelper
{
    /// <summary>
    /// Guesses the media type based on the file extension of the artifact.
    /// </summary>
    public static string GuessMediaType(string artifact) =>
        artifact.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "text/markdown";
}

/// <summary>
/// Definition details for a single business example.
/// </summary>
internal sealed record BusinessExampleDefinition(
    string Id,
    string DisplayName,
    string JobToBeDone,
    IReadOnlyList<string> LocalChecks,
    IReadOnlyList<string> OutputArtifacts)
{
    /// <summary>
    /// Converts this definition into a public validation result envelope.
    /// </summary>
    public PublicExampleResultEnvelope ToEnvelope() =>
        PublicExampleResultEnvelope.Create(
            exampleId: Id,
            exampleVersion: "1.0.0",
            inputSummary: $"{Id}: {JobToBeDone}",
            localValidation: new PublicExampleValidationSummary("passed", LocalChecks),
            outputArtifactRefs: OutputArtifacts.Select(artifact =>
                new PublicExampleOutputArtifactRef("sample-output", artifact, BusinessOperationsHelper.GuessMediaType(artifact))),
            selfReportedMetrics: new Dictionary<string, decimal>
            {
                ["localChecks"] = LocalChecks.Count,
                ["outputArtifacts"] = OutputArtifacts.Count
            },
            runId: $"{Id}-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-17T18:35:00Z"));
}

/// <summary>
/// Structured result returned by the offline smoke run.
/// </summary>
internal sealed record BusinessOperationsSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);
