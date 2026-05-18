using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke()),
    "list" => WriteJson(WritingPublishingCatalog.Examples),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static WritingPublishingSmokeResult RunSmoke()
{
    var envelopes = WritingPublishingCatalog.Examples.Select(item => item.ToEnvelope()).ToArray();
    var passed = envelopes.Length == 4 && envelopes.All(item => item.LocalValidation.IsPassed);
    return new WritingPublishingSmokeResult(passed ? "passed" : "failed", envelopes.Length, envelopes);
}

static int WriteHelp()
{
    Console.WriteLine("Writing and publishing example pack");
    Console.WriteLine("  dotnet run --project samples/writing-publishing -- --smoke");
    Console.WriteLine("  dotnet run --project samples/writing-publishing -- list");
    return 0;
}

static int WriteError(string message)
{
    Console.Error.WriteLine(message);
    return 2;
}

static int WriteJson<T>(T value, int exitCode = 0)
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
    Console.WriteLine(JsonSerializer.Serialize(value, options));
    return exitCode;
}

internal static class WritingPublishingCatalog
{
    public static IReadOnlyList<WritingExampleDefinition> Examples { get; } =
    [
        new("proposal-writer", "Proposal Writer", ["brief parsed", "scope drafted", "FAQ drafted"], ["proposal.md"]),
        new("content-repurposer", "Content Repurposer", ["key points extracted", "channel drafts created"], ["linkedin.md", "email.md"]),
        new("publishing-planner", "Publishing Planner", ["topic backlog grouped", "calendar drafted"], ["publishing-calendar.md"]),
        new("writing-assistant", "Writing Assistant", ["style profile read", "revision notes emitted"], ["revision-notes.md", "rewrite.md"])
    ];
}

internal sealed record WritingExampleDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Artifacts)
{
    public PublicExampleResultEnvelope ToEnvelope() =>
        PublicExampleResultEnvelope.Create(
            Id,
            "1.0.0",
            $"{DisplayName} public writing and publishing smoke",
            new PublicExampleValidationSummary("passed", Checks),
            Artifacts.Select(path => new PublicExampleOutputArtifactRef("sample-output", path, "text/markdown")),
            new Dictionary<string, decimal> { ["checks"] = Checks.Count, ["artifacts"] = Artifacts.Count },
            $"{Id}-offline-smoke",
            DateTimeOffset.Parse("2026-05-17T18:45:00Z"));
}

internal sealed record WritingPublishingSmokeResult(
    string Status,
    int ExampleCount,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);
