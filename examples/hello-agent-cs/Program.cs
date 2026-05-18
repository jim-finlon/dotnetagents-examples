using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => RunSmoke(),
    "hello" => WriteJson(HelloAgent.HandleHello(args.Skip(1).FirstOrDefault() ?? "DNA developer")),
    "card" => WriteJson(HelloAgent.AgentCard),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static int RunSmoke()
{
    var card = HelloAgent.AgentCard;
    var hello = HelloAgent.HandleHello("David Carter");
    var lesson = HelloAgent.RecordLesson("hello-agent-cs.smoke", "success");

    var passed = card.AgentId == "hello-agent-cs" &&
                 card.A2ARegistrationRoute == "/.well-known/agent.json" &&
                 card.McpTools.Contains("hello") &&
                 hello.Message.Contains("David Carter", StringComparison.Ordinal) &&
                 lesson.ProblemSignature == "sample:hello-agent-cs:smoke";

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
                "lesson event includes stable problem signature"
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
        lesson.ProblemSignature,
        lesson.Outcome,
        resultEnvelope = envelope
    }, passed ? 0 : 1);
}

static int WriteHelp()
{
    Console.WriteLine("Hello DNA Agent C# sample");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/hello-agent-cs -- --smoke");
    Console.WriteLine("  dotnet run --project samples/hello-agent-cs -- card");
    Console.WriteLine("  dotnet run --project samples/hello-agent-cs -- hello \"Your name\"");
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

internal static class HelloAgent
{
    public static HelloAgentCard AgentCard { get; } = new(
        AgentId: "hello-agent-cs",
        DisplayName: "Hello DNA Agent C#",
        Purpose: "Offline engineering-distribution sample that maps one tiny tool to A2A, MCP, and memory-capture concepts.",
        A2ARegistrationRoute: "/.well-known/agent.json",
        McpTools: ["hello", "card"],
        LessonEventShape: "lesson.event.v1");

    public static HelloResponse HandleHello(string name) =>
        new(
            Message: $"Hello, {name}. This sample is intentionally offline: no platform service, Tyr, credentials, or live services required.",
            McpToolName: "hello",
            A2AIntent: "agent.sample.hello",
            NextStep: "Open README.md, change the greeting, then rerun --smoke.");

    public static LessonEvent RecordLesson(string step, string outcome) =>
        new(
            ProblemSignature: "sample:hello-agent-cs:smoke",
            Service: AgentCard.AgentId,
            Step: step,
            Outcome: outcome,
            Summary: "The Hello-agent sample smoke command validated the local A2A/MCP/lesson shape without external dependencies.");
}

internal sealed record HelloAgentCard(
    string AgentId,
    string DisplayName,
    string Purpose,
    string A2ARegistrationRoute,
    IReadOnlyList<string> McpTools,
    string LessonEventShape);

internal sealed record HelloResponse(
    string Message,
    string McpToolName,
    string A2AIntent,
    string NextStep);

internal sealed record LessonEvent(
    string ProblemSignature,
    string Service,
    string Step,
    string Outcome,
    string Summary);
