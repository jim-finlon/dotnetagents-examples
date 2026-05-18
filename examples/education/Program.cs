using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke()),
    "list" => WriteJson(EducationCatalog.Examples),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static EducationSmokeResult RunSmoke()
{
    var envelopes = EducationCatalog.Examples.Select(item => item.ToEnvelope()).ToArray();
    var passed = envelopes.Length == 3 && envelopes.All(item => item.LocalValidation.IsPassed);
    return new EducationSmokeResult(passed ? "passed" : "failed", envelopes.Length, envelopes);
}

static int WriteHelp()
{
    Console.WriteLine("Education example pack");
    Console.WriteLine("  dotnet run --project samples/education -- --smoke");
    Console.WriteLine("  dotnet run --project samples/education -- list");
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

internal static class EducationCatalog
{
    public static IReadOnlyList<EducationExampleDefinition> Examples { get; } =
    [
        new("educational-tutor", "Educational Tutor", ["lesson generated", "concept check included", "mastery note emitted"], ["lesson.md", "mastery.json"]),
        new("study-planner", "Study Planner", ["goals parsed", "sessions sequenced", "review cadence emitted"], ["study-plan.md"]),
        new("quiz-coach", "Quiz Coach", ["questions drafted", "answer key generated", "feedback rubric emitted"], ["quiz.json", "feedback.md"])
    ];
}

internal sealed record EducationExampleDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> Checks,
    IReadOnlyList<string> Artifacts)
{
    public PublicExampleResultEnvelope ToEnvelope() =>
        PublicExampleResultEnvelope.Create(
            Id,
            "1.0.0",
            $"{DisplayName} public education smoke",
            new PublicExampleValidationSummary("passed", Checks),
            Artifacts.Select(path => new PublicExampleOutputArtifactRef(
                "sample-output",
                path,
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/markdown")),
            new Dictionary<string, decimal> { ["checks"] = Checks.Count, ["artifacts"] = Artifacts.Count },
            $"{Id}-offline-smoke",
            DateTimeOffset.Parse("2026-05-17T18:50:00Z"));
}

internal sealed record EducationSmokeResult(
    string Status,
    int ExampleCount,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);
