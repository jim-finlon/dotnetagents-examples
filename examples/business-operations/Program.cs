using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke()),
    "list" => WriteJson(BusinessOperationsCatalog.Examples),
    "run" => RunExample(args.Skip(1).FirstOrDefault()),
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

static int RunExample(string? exampleId)
{
    if (string.IsNullOrWhiteSpace(exampleId))
    {
        return WriteError("Usage: dotnet run --project samples/business-operations -- run <example-id>");
    }

    var example = BusinessOperationsCatalog.Examples
        .SingleOrDefault(item => string.Equals(item.Id, exampleId, StringComparison.OrdinalIgnoreCase));

    return example is null
        ? WriteError($"Unknown example '{exampleId}'. Run the 'list' command to see available examples.")
        : WriteJson(example.ToEnvelope());
}

static int WriteHelp()
{
    Console.WriteLine("Business operations example pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/business-operations -- --smoke");
    Console.WriteLine("  dotnet run --project samples/business-operations -- list");
    Console.WriteLine("  dotnet run --project samples/business-operations -- run project-planner");
    return 0;
}

static int WriteError(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

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

internal static class BusinessOperationsCatalog
{
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

internal sealed record BusinessExampleDefinition(
    string Id,
    string DisplayName,
    string JobToBeDone,
    IReadOnlyList<string> LocalChecks,
    IReadOnlyList<string> OutputArtifacts)
{
    public PublicExampleResultEnvelope ToEnvelope() =>
        PublicExampleResultEnvelope.Create(
            exampleId: Id,
            exampleVersion: "1.0.0",
            inputSummary: $"{Id}: {JobToBeDone}",
            localValidation: new PublicExampleValidationSummary("passed", LocalChecks),
            outputArtifactRefs: OutputArtifacts.Select(artifact =>
                new PublicExampleOutputArtifactRef("sample-output", artifact, GuessMediaType(artifact))),
            selfReportedMetrics: new Dictionary<string, decimal>
            {
                ["localChecks"] = LocalChecks.Count,
                ["outputArtifacts"] = OutputArtifacts.Count
            },
            runId: $"{Id}-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-17T18:35:00Z"));

    private static string GuessMediaType(string artifact) =>
        artifact.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "text/markdown";
}

internal sealed record BusinessOperationsSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);
