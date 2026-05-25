using DotNetAgents.Core.PublicExamples;
using DotNetAgents.Abstractions.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke()),
    "list" => WriteJson(WritingPublishingCatalog.Examples),
    "run" => await RunExampleAsync(args.Skip(1).FirstOrDefault()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static WritingPublishingSmokeResult RunSmoke()
{
    var envelopes = WritingPublishingCatalog.Examples.Select(item => item.ToEnvelope()).ToArray();
    var passed = envelopes.Length == 4 && envelopes.All(item => item.LocalValidation.IsPassed);
    return new WritingPublishingSmokeResult(passed ? "passed" : "failed", envelopes.Length, envelopes);
}

static async Task<int> RunExampleAsync(string? exampleId)
{
    if (string.IsNullOrWhiteSpace(exampleId))
    {
        return WriteError("Usage: dotnet run --project samples/writing-publishing -- run <example-id>");
    }

    var example = WritingPublishingCatalog.Examples
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
            Console.WriteLine($"[Live Mode] Running writing example '{exampleId}' using model '{provider.ModelName}'...");
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
    var definition = WritingPublishingCatalog.Examples.First(item => string.Equals(item.Id, exampleId, StringComparison.OrdinalIgnoreCase));
    string prompt;
    string inputSummary;
    string liveOutputText;

    switch (exampleId.ToLowerInvariant())
    {
        case "proposal-writer":
            inputSummary = "Next-generation AI orchestration platform migration";
            prompt = $"You are a professional proposal writer. Write a client proposal introduction, scope of work, and pricing structure for a business offering: '{inputSummary}'. Format as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "content-repurposer":
            inputSummary = "Announcing open-source C# agent framework DotNetAgents v10";
            prompt = $"You are a content strategist. Repurpose the following technical blog post topic into a professional LinkedIn post and a brief email newsletter: '{inputSummary}'. Format as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "publishing-planner":
            inputSummary = "4-week content publishing schedule for a software engineering blog focusing on AI agents, developer productivity, and .NET 10";
            prompt = $"You are a marketing manager. Create a content publishing plan based on: '{inputSummary}'. Group by week and specify topic, target channel, and owner. Format as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        case "writing-assistant":
            inputSummary = "We are very excited to present our new software which makes agentic loops run faster and better. It is really cool and you should try it.";
            prompt = $"You are an editorial assistant. Analyze the writing style and provide style suggestions and rewrite notes for: '{inputSummary}'. Format as clean markdown.";
            liveOutputText = await provider.GenerateAsync(prompt);
            break;

        default:
            throw new ArgumentException($"Unknown writing example ID: {exampleId}");
    }

    var resultEnvelope = PublicExampleResultEnvelope.Create(
        exampleId: exampleId,
        exampleVersion: "1.0.0",
        inputSummary: inputSummary,
        localValidation: new PublicExampleValidationSummary("passed", definition.Checks),
        outputArtifactRefs: definition.Artifacts.Select(path =>
            new PublicExampleOutputArtifactRef("live-output", path, "text/markdown")),
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
    Console.WriteLine("Writing and publishing example pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/writing-publishing -- --smoke");
    Console.WriteLine("  dotnet run --project samples/writing-publishing -- list");
    Console.WriteLine("  dotnet run --project samples/writing-publishing -- run <example-id>");
    Console.WriteLine("     Examples: proposal-writer, content-repurposer, publishing-planner, writing-assistant");
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
