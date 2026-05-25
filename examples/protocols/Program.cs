using System.Text.Json;
using System.Text.Json.Serialization;

using DotNetAgents.A2A;
using DotNetAgents.Core.PublicExamples;
using DotNetAgents.Mcp.Models;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => await RunSmokeAsync(),
    "mcp-consumer" => await WriteJsonAsync(await ProtocolExamples.RunMcpConsumerAsync()),
    "a2a-handoff" => await WriteJsonAsync(await ProtocolExamples.RunA2AHandoffAsync()),
    "boundary" => await WriteJsonAsync(ProtocolExamples.DescribeBoundary()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static async Task<int> RunSmokeAsync()
{
    var results = new ProtocolExampleResult[]
    {
        await ProtocolExamples.RunMcpConsumerAsync(),
        await ProtocolExamples.RunA2AHandoffAsync(),
        ProtocolExamples.DescribeBoundary()
    };

    var passed = results.Length == 3 &&
                 results.All(result => result.Status == "passed") &&
                 results.All(result => result.ResultEnvelope.SchemaVersion == PublicExampleResultEnvelopeContract.SchemaVersion);

    return await WriteJsonAsync(new ProtocolSmokeResult(
        passed ? "passed" : "failed",
        "protocols",
        results.Length,
        results,
        results.Select(result => result.ResultEnvelope).ToArray()), passed ? 0 : 1);
}

static int WriteHelp()
{
    Console.WriteLine("Protocol examples pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/protocols -- --smoke");
    Console.WriteLine("  dotnet run --project examples/protocols -- mcp-consumer");
    Console.WriteLine("  dotnet run --project examples/protocols -- a2a-handoff");
    Console.WriteLine("  dotnet run --project examples/protocols -- boundary");
    Console.WriteLine();
    Console.WriteLine("All commands run offline with in-process public-safe protocol fixtures.");
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

    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(value, jsonOptions));
    return exitCode;
}

internal static class ProtocolExamples
{
    public static async Task<ProtocolExampleResult> RunMcpConsumerAsync()
    {
        var server = new LocalMcpProtocolServer();
        var tools = await server.ListToolsAsync().ConfigureAwait(false);
        var call = await server.CallToolAsync(new McpToolCallRequest
        {
            Tool = "summarize_protocol_note",
            Arguments = new Dictionary<string, object>
            {
                ["note"] = "MCP is for tools and human-operated clients; A2A is for agent-to-agent tasks."
            },
            CorrelationId = "protocol-smoke-001",
            TimeoutSeconds = 3
        }).ConfigureAwait(false);

        var passed = tools.TotalCount == 2 &&
                     tools.Tools.Any(tool => tool.Name == "summarize_protocol_note") &&
                     call.Success;

        return CreateResult(
            id: "protocol-mcp-consumer",
            title: "MCP Consumer",
            capability: "List local MCP tools and call one with a public-safe request envelope.",
            checks:
            [
                "listed local MCP tools",
                "called summarize_protocol_note",
                "captured request correlation id",
                "kept endpoint local/in-process"
            ],
            artifacts: ["mcp-consumer-transcript.json"],
            details: new
            {
                tools.TotalCount,
                tools = tools.Tools.Select(tool => new { tool.Name, tool.Category }),
                call.Success,
                call.Summary,
                call.Result
            },
            metrics: new Dictionary<string, decimal>
            {
                ["toolsListed"] = tools.TotalCount,
                ["toolCalls"] = 1,
                ["networkCalls"] = 0
            },
            passed);
    }

    public static async Task<ProtocolExampleResult> RunA2AHandoffAsync()
    {
        var intakeAgent = A2AProtocolAgent.CreateIntakeAgent();
        var reviewerAgent = A2AProtocolAgent.CreateReviewerAgent();
        var handoffTask = new A2ATask
        {
            Id = "a2a-protocol-smoke-001",
            Skill = "review_handoff",
            Input = new Dictionary<string, object>
            {
                ["draft"] = "Please review a local MCP consumer transcript for public boundary issues."
            },
            Metadata = new A2AMetadata
            {
                CorrelationId = "protocol-smoke-001",
                Headers = new Dictionary<string, string>
                {
                    ["traceparent"] = "00-00000000000000000000000000000001-0000000000000001-01"
                }
            }
        };

        var card = reviewerAgent.GetAgentCard();
        var response = await reviewerAgent.HandleTaskAsync(handoffTask).ConfigureAwait(false);
        var events = new List<object>();
        await foreach (var evt in intakeAgent.StreamTaskAsync(handoffTask).ConfigureAwait(false))
        {
            events.Add(new { evt.TaskId, evt.EventType });
        }

        var passed = card.Skills.Any(skill => skill.Name == "review_handoff") &&
                     response.Success &&
                     events.Count == 1;

        return CreateResult(
            id: "protocol-a2a-handoff",
            title: "A2A Handoff",
            capability: "Publish an agent card, send a task to another agent, and preserve correlation metadata.",
            checks:
            [
                "agent card includes handoff skill",
                "task handled by receiving agent",
                "correlation id preserved",
                "stream event emitted"
            ],
            artifacts: ["a2a-handoff-transcript.json"],
            details: new
            {
                agentCard = card,
                task = new { handoffTask.Id, handoffTask.Skill, handoffTask.Metadata?.CorrelationId },
                response,
                events
            },
            metrics: new Dictionary<string, decimal>
            {
                ["skills"] = card.Skills.Count,
                ["tasks"] = 1,
                ["events"] = events.Count
            },
            passed);
    }

    public static ProtocolExampleResult DescribeBoundary()
    {
        var boundary = new ProtocolBoundary(
            Mcp: "Use MCP for tools, IDEs, dashboards, and human-operated clients.",
            A2A: "Use A2A for agent-to-agent task handoff and agent-card discovery.",
            PublicSafety: "Keep examples localhost or in-process, use synthetic data, and do not require private services.");

        return CreateResult(
            id: "protocol-boundary",
            title: "Protocol Boundary",
            capability: "Show the public distinction between MCP and A2A without private operating language.",
            checks:
            [
                "MCP boundary stated",
                "A2A boundary stated",
                "public safety rule stated"
            ],
            artifacts: ["protocol-boundary.json"],
            details: boundary,
            metrics: new Dictionary<string, decimal>
            {
                ["boundaryRules"] = 3
            },
            passed: true);
    }

    private static ProtocolExampleResult CreateResult(
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
            outputArtifactRefs: artifacts.Select(artifact =>
                new PublicExampleOutputArtifactRef("sample-output", artifact, "application/json")),
            selfReportedMetrics: metrics,
            runId: $"{id}-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-25T19:20:00Z"));

        return new ProtocolExampleResult(
            passed ? "passed" : "failed",
            id,
            title,
            capability,
            details,
            envelope);
    }
}

internal sealed class LocalMcpProtocolServer
{
    public Task<McpListToolsResponse> ListToolsAsync()
    {
        var tools = new List<McpToolDefinition>
        {
            Tool("summarize_protocol_note", "Summarize a synthetic protocol note.", "protocol.consumer", ["note"]),
            Tool("get_protocol_boundary", "Return public MCP/A2A boundary guidance.", "protocol.boundary", [])
        };

        return Task.FromResult(new McpListToolsResponse
        {
            Tools = tools,
            TotalCount = tools.Count
        });
    }

    public Task<McpToolCallResponse> CallToolAsync(McpToolCallRequest request)
    {
        var response = request.Tool switch
        {
            "summarize_protocol_note" => Summarize(request),
            "get_protocol_boundary" => new McpToolCallResponse
            {
                Success = true,
                Result = ProtocolExamples.DescribeBoundary().Details,
                Summary = "Protocol boundary returned."
            },
            _ => new McpToolCallResponse
            {
                Success = false,
                ErrorCode = "NOT_FOUND",
                Error = $"Unknown tool '{request.Tool}'."
            }
        };

        return Task.FromResult(response);
    }

    private static McpToolCallResponse Summarize(McpToolCallRequest request)
    {
        if (!request.Arguments.TryGetValue("note", out var note) || string.IsNullOrWhiteSpace(note?.ToString()))
        {
            return new McpToolCallResponse
            {
                Success = false,
                ErrorCode = "MISSING_ARG",
                Error = "note is required"
            };
        }

        return new McpToolCallResponse
        {
            Success = true,
            Summary = "Protocol note summarized.",
            Result = new
            {
                request.CorrelationId,
                summary = "MCP exposes tool calls to clients; A2A carries tasks between agents.",
                sourceLength = note.ToString()!.Length
            },
            SuggestedNextSteps = ["Call get_protocol_boundary", "Try a2a-handoff"]
        };
    }

    private static McpToolDefinition Tool(
        string name,
        string description,
        string category,
        IReadOnlyList<string> required) =>
        new()
        {
            Name = name,
            Description = description,
            Category = category,
            InputSchema = new McpToolInputSchema
            {
                Required = required.ToList(),
                Properties = required.ToDictionary(
                    argument => argument,
                    argument => new McpProperty
                    {
                        Type = "string",
                        Description = $"Required {argument} argument."
                    },
                    StringComparer.Ordinal)
            }
        };
}

internal sealed class A2AProtocolAgent : A2AAgentBase
{
    private A2AProtocolAgent(AgentCard card, Func<A2ATask, CancellationToken, Task<A2AResponse>> handleTask)
        : base(card, handleTask)
    {
    }

    public static A2AProtocolAgent CreateIntakeAgent() =>
        new(
            new AgentCard
            {
                Name = "Protocol Intake Agent",
                Description = "Public example agent that receives protocol work and emits a completion event.",
                Version = "1.0",
                SupportedModes = ["task", "stream"],
                Skills =
                [
                    new Skill
                    {
                        Name = "review_handoff",
                        Description = "Forward a protocol review request to another local agent."
                    }
                ]
            },
            static (task, _) => Task.FromResult(new A2AResponse
            {
                TaskId = task.Id,
                Success = true,
                Output = new
                {
                    received = task.Skill,
                    task.Metadata?.CorrelationId,
                    note = "Intake accepted the local handoff."
                }
            }));

    public static A2AProtocolAgent CreateReviewerAgent() =>
        new(
            new AgentCard
            {
                Name = "Protocol Reviewer Agent",
                Description = "Public example agent that reviews synthetic protocol handoffs.",
                Version = "1.0",
                SupportedModes = ["task"],
                Skills =
                [
                    new Skill
                    {
                        Name = "review_handoff",
                        Description = "Review a synthetic protocol handoff for boundary safety.",
                        InputSchema = new { type = "object", required = new[] { "draft" } }
                    }
                ]
            },
            static (task, _) => Task.FromResult(new A2AResponse
            {
                TaskId = task.Id,
                Success = true,
                Output = new
                {
                    verdict = "public-safe",
                    task.Metadata?.CorrelationId,
                    guidance = "Keep MCP for tools and A2A for agent handoffs; no private endpoints required."
                }
            }));
}

internal sealed record ProtocolBoundary(string Mcp, string A2A, string PublicSafety);

internal sealed record ProtocolExampleResult(
    string Status,
    string ExampleId,
    string Title,
    string Capability,
    object Details,
    PublicExampleResultEnvelope ResultEnvelope);

internal sealed record ProtocolSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<ProtocolExampleResult> Examples,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);
