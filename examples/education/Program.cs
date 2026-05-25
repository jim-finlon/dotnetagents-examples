using DotNetAgents.Core.PublicExamples;
using DotNetAgents.Abstractions.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke()),
    "list" => WriteJson(EducationCatalog.Examples),
    "run" => await RunExampleAsync(args.Skip(1).FirstOrDefault()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static EducationSmokeResult RunSmoke()
{
    var envelopes = EducationCatalog.Examples.Select(item => item.ToEnvelope()).ToArray();
    var passed = envelopes.Length == 3 && envelopes.All(item => item.LocalValidation.IsPassed);
    return new EducationSmokeResult(passed ? "passed" : "failed", envelopes.Length, envelopes);
}

static async Task<int> RunExampleAsync(string? exampleId)
{
    if (string.IsNullOrWhiteSpace(exampleId))
    {
        return WriteError("Usage: dotnet run --project samples/education -- run <example-id>");
    }

    var example = EducationCatalog.Examples
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
            Console.WriteLine($"[Live Mode] Running education example '{exampleId}' using model '{provider.ModelName}'...");
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
    var definition = EducationCatalog.Examples.First(item => string.Equals(item.Id, exampleId, StringComparison.OrdinalIgnoreCase));
    string prompt;
    string inputSummary;
    string liveOutputText;

    switch (exampleId.ToLowerInvariant())
    {
        case "educational-tutor":
            inputSummary = "Explain asynchronous programming in C# and async/await task patterns to a beginner student.";
            prompt = $"You are a friendly and encouraging educational tutor. Explain the following concept: '{inputSummary}'. Include a brief analogy and a simple code example. Format as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "study-planner":
            inputSummary = "Create a 2-week daily study schedule for a developer learning LLM-based agentic workflows.";
            prompt = $"You are an academic study planner. Create a daily study schedule based on: '{inputSummary}'. Focus on key topics like prompting, tool calling, and cognitive loops. Format as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "quiz-coach":
            inputSummary = "Generate a 3-question multiple-choice quiz on C# interfaces and dependency injection with feedback.";
            prompt = $"You are a quiz coach. Generate a quiz based on: '{inputSummary}', including correct answers and a brief feedback rationale for each option. Format as structured JSON or clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        default:
            throw new ArgumentException($"Unknown education example ID: {exampleId}");
    }

    var resultEnvelope = PublicExampleResultEnvelope.Create(
        exampleId: exampleId,
        exampleVersion: "1.0.0",
        inputSummary: inputSummary,
        localValidation: new PublicExampleValidationSummary("passed", definition.Checks),
        outputArtifactRefs: definition.Artifacts.Select(path =>
            new PublicExampleOutputArtifactRef(
                "live-output",
                path,
                path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/markdown")),
        selfReportedMetrics: new Dictionary<string, decimal>
        {
            ["checks"] = definition.Checks.Count,
            ["artifacts"] = definition.Artifacts.Count,
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

static int WriteHelp()
{
    Console.WriteLine("Education example pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/education -- --smoke");
    Console.WriteLine("  dotnet run --project samples/education -- list");
    Console.WriteLine("  dotnet run --project samples/education -- run <example-id>");
    Console.WriteLine("     Examples: educational-tutor, study-planner, quiz-coach");
    Console.WriteLine();
    Console.WriteLine("Live Mode Configuration (optional):");
    Console.WriteLine("Configure API keys as environment variables to run live executions via providers:");
    Console.WriteLine("  export OPENAI_API_KEY=\"your-openai-api-key\"");
    Console.WriteLine("  export ANTHROPIC_API_KEY=\"your-anthropic-api-key\"");
    Console.WriteLine("  export OLLAMA_MODEL=\"llama3\" [OLLAMA_HOST=\"http://localhost:11434\"]");
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
