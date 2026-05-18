using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke(), 0),
    "list" => WriteJson(PublicEntrepreneurCatalog.Examples),
    "run" => RunExample(args.Skip(1).FirstOrDefault()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static PublicExampleSmokeResult RunSmoke()
{
    var envelopes = PublicEntrepreneurCatalog.Examples
        .Select(example => example.ToEnvelope())
        .ToArray();

    var expectedIds = new[]
    {
        "research-assistant",
        "meeting-summarizer",
        "local-knowledge-base",
        "proposal-writer",
        "invoice-helper",
        "content-repurposer",
        "customer-support-triage",
        "writing-assistant",
        "educational-tutor"
    };

    var passed = envelopes.Length == expectedIds.Length &&
                 expectedIds.All(id => envelopes.Any(envelope => envelope.ExampleId == id)) &&
                 envelopes.All(envelope => envelope.SchemaVersion == PublicExampleResultEnvelopeContract.SchemaVersion) &&
                 envelopes.All(envelope => envelope.LocalValidation.IsPassed);

    return new PublicExampleSmokeResult(
        passed ? "passed" : "failed",
        "public-entrepreneur-examples",
        envelopes.Length,
        envelopes);
}

static int RunExample(string? exampleId)
{
    if (string.IsNullOrWhiteSpace(exampleId))
    {
        return WriteError("Usage: dotnet run --project samples/public-entrepreneur-examples -- run <example-id>");
    }

    var example = PublicEntrepreneurCatalog.Examples
        .SingleOrDefault(item => string.Equals(item.Id, exampleId, StringComparison.OrdinalIgnoreCase));

    return example is null
        ? WriteError($"Unknown example '{exampleId}'. Run the 'list' command to see available examples.")
        : WriteJson(example.ToEnvelope());
}

static int WriteHelp()
{
    Console.WriteLine("Public entrepreneur example pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/public-entrepreneur-examples -- --smoke");
    Console.WriteLine("  dotnet run --project samples/public-entrepreneur-examples -- list");
    Console.WriteLine("  dotnet run --project samples/public-entrepreneur-examples -- run research-assistant");
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

internal static class PublicEntrepreneurCatalog
{
    public static IReadOnlyList<PublicExampleDefinition> Examples { get; } =
    [
        new(
            "research-assistant",
            "Research Assistant",
            "Drafts a one-page cited research brief from public notes and source snippets.",
            ["plan source search", "extract citation snippets", "assemble markdown brief"],
            ["brief.md", "sources.json"]),
        new(
            "meeting-summarizer",
            "Meeting Summarizer",
            "Turns a transcript into decisions, action items, and open questions.",
            ["read transcript", "extract decisions", "assign action items"],
            ["summary.md", "action-items.json"]),
        new(
            "local-knowledge-base",
            "Local Knowledge-Base Assistant",
            "Indexes local markdown notes and answers with file-backed citations.",
            ["scan markdown folder", "chunk notes", "answer with citations"],
            ["answer.md", "citations.json"]),
        new(
            "proposal-writer",
            "Proposal Writer",
            "Converts a client brief into scope, timeline, pricing options, and FAQ sections.",
            ["read brief", "draft scope", "build pricing options", "assemble proposal"],
            ["proposal.md", "pricing-options.json"]),
        new(
            "invoice-helper",
            "Invoice Helper",
            "Normalizes sample invoice fields into a bookkeeping-friendly CSV shape.",
            ["parse invoice text", "validate totals", "export csv row"],
            ["invoice-record.json", "invoices.csv"]),
        new(
            "content-repurposer",
            "Content Repurposer",
            "Turns one long-form post into channel-specific derivative drafts.",
            ["extract key points", "draft channel variants", "run consistency check"],
            ["linkedin.md", "email.md", "short-post.md"]),
        new(
            "customer-support-triage",
            "Customer-Support Triage",
            "Classifies support messages and drafts a response with escalation notes.",
            ["classify ticket", "choose response template", "mark escalation risk"],
            ["triage.json", "draft-response.md"]),
        new(
            "writing-assistant",
            "Writing Assistant",
            "Applies a local style profile to critique and rewrite a draft.",
            ["read style profile", "critique draft", "rewrite draft"],
            ["critique.md", "rewrite.md"]),
        new(
            "educational-tutor",
            "Educational Tutor",
            "Runs a short adaptive lesson with concept checks and mastery notes.",
            ["explain concept", "ask check question", "summarize mastery"],
            ["lesson.md", "mastery.json"])
    ];
}

internal sealed record PublicExampleDefinition(
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
            timestampUtc: DateTimeOffset.Parse("2026-05-17T18:30:00Z"));

    private static string GuessMediaType(string artifact) =>
        artifact.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : artifact.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? "text/csv"
                : "text/markdown";
}

internal sealed record PublicExampleSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);
