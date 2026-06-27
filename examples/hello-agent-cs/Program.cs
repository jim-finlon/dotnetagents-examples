using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => RunSmoke(),
    "hello" => await RunHelloAsync(args.Skip(1).FirstOrDefault() ?? "DNA developer"),
    "card" => WriteJson(HelloAgent.AgentCard),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static async Task<int> RunHelloAsync(string name)
{
    var provider = PublicLlmProvider.TryCreateFromEnv();
    if (provider != null)
    {
        try
        {
            Console.WriteLine($"[Live Mode] Generating greeting using LLM model '{provider.ModelName}'...");
            var prompt = $"Write a professional, inspiring greeting to a developer named {name} who is getting started with the DotNetAgents C# framework. Mention that they are running hello-agent-cs in live execution mode. Keep it to one paragraph.";
            var greeting = await provider.GenerateAsync(prompt);
            return WriteJson(new HelloResponse(
                Message: greeting.Trim(),
                McpToolName: "hello",
                A2AIntent: "agent.sample.hello",
                NextStep: "Try adding more custom prompts or check out other examples in examples/"
            ));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Error] Live completion failed: {ex.Message}");
            Console.Error.WriteLine("Falling back to offline mode.");
        }
    }

    return WriteJson(HelloAgent.HandleHello(name));
}

static int RunSmoke()
{
    var card = HelloAgent.AgentCard;
    var hello = HelloAgent.HandleHello("David Carter");
    var learning = HelloAgent.RecordLearningEvent("hello-agent-cs.smoke", "success");

    var passed = card.AgentId == "hello-agent-cs" &&
                 card.A2ARegistrationRoute == "/.well-known/agent.json" &&
                 card.McpTools.Contains("hello") &&
                 hello.Message.Contains("David Carter", StringComparison.Ordinal) &&
                 learning.ProblemKey == "sample:hello-agent-cs:smoke";

    var envelope = PublicExampleResultEnvelope.Create(
        exampleId: card.AgentId,
        exampleVersion: "1.0.0",
        inputSummary: "hello-agent-cs --smoke David Carter",
        localValidation: new PublicExampleValidationSummary(
            passed ? "passed" : "failed",
            [
                "agent card route present",
                "mcp tool list includes hello",
                "hello response contains requested name",
                "learning event includes stable problem key"
            ]),
        outputArtifactRefs:
        [
            new PublicExampleOutputArtifactRef("stdout", "console", "application/json")
        ],
        selfReportedMetrics: new Dictionary<string, decimal>
        {
            ["checksPassed"] = passed ? 4 : 0
        },
        runId: "hello-agent-cs-smoke",
        timestampUtc: DateTimeOffset.Parse("2026-05-17T18:20:00Z"));

    return WriteJson(new
    {
        status = passed ? "passed" : "failed",
        card.AgentId,
        card.A2ARegistrationRoute,
        card.McpTools,
        hello.Message,
        learning.ProblemKey,
        learning.Outcome,
        resultEnvelope = envelope
    }, passed ? 0 : 1);
}

static int WriteHelp()
{
    Console.WriteLine("Hello DNA Agent C# sample");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/hello-agent-cs -- --smoke");
    Console.WriteLine("  dotnet run --project examples/hello-agent-cs -- card");
    Console.WriteLine("  dotnet run --project examples/hello-agent-cs -- hello \"Your name\"");
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
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
    Console.WriteLine(JsonSerializer.Serialize(value, jsonOptions));
    return exitCode;
}

/// <summary>
/// Domain model and business logic representing the HelloAgent.
/// </summary>
internal static class HelloAgent
{
    /// <summary>
    /// Gets the metadata registration card for the hello agent.
    /// </summary>
    public static HelloAgentCard AgentCard { get; } = new(
        AgentId: "hello-agent-cs",
        DisplayName: "Hello DNA Agent C#",
        Purpose: "Offline engineering-distribution sample that maps one tiny tool to A2A, MCP, and learning-event concepts.",
        A2ARegistrationRoute: "/.well-known/agent.json",
        McpTools: ["hello", "card"],
        LearningEventShape: "learning.event.v1");

    /// <summary>
    /// Generates a standard offline greeting message.
    /// </summary>
    /// <param name="name">The name of the user to greet.</param>
    /// <returns>A structured HelloResponse.</returns>
    public static HelloResponse HandleHello(string name) =>
        new(
            Message: $"Hello, {name}. This sample is intentionally offline: no private control plane, credentials, or live services required.",
            McpToolName: "hello",
            A2AIntent: "agent.sample.hello",
            NextStep: "Open README.md, change the greeting, then rerun --smoke.");

    /// <summary>
    /// Records a deterministic learning event for local telemetry or audit validation.
    /// </summary>
    /// <param name="step">The execution step name.</param>
    /// <param name="outcome">The execution outcome status.</param>
    /// <returns>A structured LearningEvent.</returns>
    public static LearningEvent RecordLearningEvent(string step, string outcome) =>
        new(
            ProblemKey: "sample:hello-agent-cs:smoke",
            Service: AgentCard.AgentId,
            Step: step,
            Outcome: outcome,
            Summary: "The Hello-agent sample smoke command validated the local A2A/MCP/learning-event shape without external dependencies.");
}

/// <summary>
/// Metadata registration card for a public example agent.
/// </summary>
internal sealed record HelloAgentCard(
    string AgentId,
    string DisplayName,
    string Purpose,
    string A2ARegistrationRoute,
    IReadOnlyList<string> McpTools,
    string LearningEventShape);

/// <summary>
/// Structured greeting response.
/// </summary>
internal sealed record HelloResponse(
    string Message,
    string McpToolName,
    string A2AIntent,
    string NextStep);

/// <summary>
/// Telemetry learning event payload.
/// </summary>
internal sealed record LearningEvent(
    string ProblemKey,
    string Service,
    string Step,
    string Outcome,
    string Summary);
