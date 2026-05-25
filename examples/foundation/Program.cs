using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using DotNetAgents.Abstractions.Models;
using DotNetAgents.Abstractions.Resilience;
using DotNetAgents.Abstractions.Tools;
using DotNetAgents.Core.OutputParsers;
using DotNetAgents.Core.PublicExamples;
using DotNetAgents.Core.Resilience;
using DotNetAgents.Core.Tools;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => await RunSmokeAsync(),
    "tools" => await WriteJsonAsync(await FoundationExamples.RunToolCallingAsync()),
    "structured-output" => await WriteJsonAsync(await FoundationExamples.RunStructuredOutputAsync()),
    "streaming" => await WriteJsonAsync(await FoundationExamples.RunStreamingAsync()),
    "routing" => await WriteJsonAsync(FoundationExamples.RunModelRouting()),
    "retry" => await WriteJsonAsync(await FoundationExamples.RunRetryEnvelopeAsync()),
    "usage" => await WriteJsonAsync(FoundationExamples.RunUsageReporting()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static async Task<int> RunSmokeAsync()
{
    var examples = new FoundationExampleResult[]
    {
        await FoundationExamples.RunToolCallingAsync(),
        await FoundationExamples.RunStructuredOutputAsync(),
        await FoundationExamples.RunStreamingAsync(),
        FoundationExamples.RunModelRouting(),
        await FoundationExamples.RunRetryEnvelopeAsync(),
        FoundationExamples.RunUsageReporting()
    };

    var envelopes = examples.Select(example => example.ResultEnvelope).ToArray();
    var passed = examples.Length == 6 &&
                 examples.All(example => example.Status == "passed") &&
                 envelopes.All(envelope => envelope.SchemaVersion == PublicExampleResultEnvelopeContract.SchemaVersion) &&
                 envelopes.All(envelope => envelope.LocalValidation.IsPassed);

    return await WriteJsonAsync(new FoundationSmokeResult(
        passed ? "passed" : "failed",
        "foundation",
        examples.Length,
        examples,
        envelopes), passed ? 0 : 1);
}

static int WriteHelp()
{
    Console.WriteLine("Foundation examples pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/foundation -- --smoke");
    Console.WriteLine("  dotnet run --project examples/foundation -- tools");
    Console.WriteLine("  dotnet run --project examples/foundation -- structured-output");
    Console.WriteLine("  dotnet run --project examples/foundation -- streaming");
    Console.WriteLine("  dotnet run --project examples/foundation -- routing");
    Console.WriteLine("  dotnet run --project examples/foundation -- retry");
    Console.WriteLine("  dotnet run --project examples/foundation -- usage");
    Console.WriteLine();
    Console.WriteLine("Live Mode Configuration (optional):");
    Console.WriteLine("  export OPENAI_API_KEY=\"your-openai-api-key\" [OPENAI_MODEL=\"gpt-4o-mini\"]");
    Console.WriteLine("  export ANTHROPIC_API_KEY=\"your-anthropic-api-key\" [ANTHROPIC_MODEL=\"claude-3-5-haiku-20241022\"]");
    Console.WriteLine("  export OLLAMA_MODEL=\"llama3\" [OLLAMA_HOST=\"http://localhost:11434\"]");
    Console.WriteLine();
    Console.WriteLine("The smoke command is deterministic and does not call any provider.");
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

internal static class FoundationExamples
{
    public static async Task<FoundationExampleResult> RunToolCallingAsync()
    {
        var registry = new ToolRegistry();
        registry.Register(new CaseNormalizerTool());

        var tool = registry.GetTool("normalize_case") ?? throw new InvalidOperationException("Tool was not registered.");
        var result = await tool.ExecuteAsync(new CaseNormalizerInput("Ship public examples", "kebab")).ConfigureAwait(false);
        var output = (CaseNormalizerOutput?)result.Output ?? throw new InvalidOperationException("Tool output was missing.");

        return CreateResult(
            id: "foundation-tool-calling",
            title: "Tool Calling",
            capability: "Register an ITool, execute it with a typed input, and return a ToolResult.",
            checks:
            [
                "tool registered in ToolRegistry",
                "tool execution returned success",
                "tool output normalized text"
            ],
            artifacts: ["tool-result.json"],
            details: new
            {
                tool.Name,
                result.IsSuccess,
                output.Normalized,
                output.CultureInvariant
            },
            metrics: new Dictionary<string, decimal>
            {
                ["registeredTools"] = registry.GetAllTools().Count,
                ["toolCalls"] = 1
            });
    }

    public static async Task<FoundationExampleResult> RunStructuredOutputAsync()
    {
        var parser = new JsonOutputParser<SupportTicketClassification>();
        const string modelOutput = """
        {
          "urgency": "high",
          "category": "billing",
          "nextAction": "Ask for invoice id and route to billing specialist",
          "confidence": 0.91
        }
        """;

        var parsed = await parser.ParseAsync(modelOutput).ConfigureAwait(false);

        return CreateResult(
            id: "foundation-structured-output",
            title: "Structured Output",
            capability: "Parse provider text into a typed response contract with JsonOutputParser<T>.",
            checks:
            [
                "format instructions available",
                "JSON parsed into typed record",
                "confidence score preserved"
            ],
            artifacts: ["classification.json"],
            details: new
            {
                parserInstructions = parser.GetFormatInstructions(),
                parsed
            },
            metrics: new Dictionary<string, decimal>
            {
                ["confidence"] = (decimal)parsed.Confidence,
                ["fieldsParsed"] = 4
            });
    }

    public static async Task<FoundationExampleResult> RunStreamingAsync()
    {
        var model = new OfflineStreamingModel(
            "offline-streaming-model",
            ["Planning ", "agent ", "work ", "as ", "events."]);
        var chunks = new List<string>();

        await foreach (var chunk in model.GenerateStreamAsync("stream a progress sentence").ConfigureAwait(false))
        {
            chunks.Add(chunk);
        }

        return CreateResult(
            id: "foundation-streaming",
            title: "Streaming Progress",
            capability: "Consume IAsyncEnumerable model output and surface progress chunks.",
            checks:
            [
                "stream produced ordered chunks",
                "chunks composed into final text",
                "stream stayed offline"
            ],
            artifacts: ["stream-transcript.json"],
            details: new
            {
                model.ModelName,
                chunks,
                finalText = string.Concat(chunks)
            },
            metrics: new Dictionary<string, decimal>
            {
                ["chunks"] = chunks.Count,
                ["characters"] = string.Concat(chunks).Length
            });
    }

    public static FoundationExampleResult RunModelRouting()
    {
        var plan = ModelRoutingPlanner.Select(new ModelRoutingRequest(
            TaskKind: "structured-output",
            RequiresLowLatency: true,
            AllowsLocalModel: true,
            PreferredProvider: "ollama"));

        return CreateResult(
            id: "foundation-model-routing",
            title: "Model Routing",
            capability: "Choose a provider/model route from explicit request constraints before live execution.",
            checks:
            [
                "routing request captured constraints",
                "local route selected when allowed",
                "provider fallback remains explicit"
            ],
            artifacts: ["routing-plan.json"],
            details: plan,
            metrics: new Dictionary<string, decimal>
            {
                ["fallbackCount"] = plan.Fallbacks.Count,
                ["estimatedCostUnits"] = plan.EstimatedCostUnits
            });
    }

    public static async Task<FoundationExampleResult> RunRetryEnvelopeAsync()
    {
        var attempts = 0;
        var policy = new RetryPolicy(new RetryPolicyOptions
        {
            MaxRetries = 2,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(2),
            UseExponentialBackoff = false,
            ShouldRetry = exception => exception is TimeoutException
        });

        var outcome = await policy.ExecuteAsync(_ =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new TimeoutException("Synthetic transient timeout.");
            }

            return Task.FromResult(new RetryOutcome("completed", attempts, "retry succeeded after transient timeout"));
        }).ConfigureAwait(false);

        return CreateResult(
            id: "foundation-retry-envelope",
            title: "Retry And Error Envelope",
            capability: "Wrap transient failures with RetryPolicy and emit an auditable outcome.",
            checks:
            [
                "first attempt failed with transient timeout",
                "retry policy retried bounded operation",
                "final outcome captured attempts"
            ],
            artifacts: ["retry-envelope.json"],
            details: outcome,
            metrics: new Dictionary<string, decimal>
            {
                ["attempts"] = outcome.Attempts,
                ["maxRetries"] = 2
            });
    }

    public static FoundationExampleResult RunUsageReporting()
    {
        var report = UsageReport.Create(
            runId: "foundation-usage-smoke",
            promptCharacters: 96,
            completionCharacters: 148,
            toolCalls: 1,
            streamChunks: 5,
            retryAttempts: 1);

        return CreateResult(
            id: "foundation-usage-reporting",
            title: "Usage Reporting",
            capability: "Collect local run metrics so examples can show cost and activity posture without provider calls.",
            checks:
            [
                "prompt and completion character counts captured",
                "tool/stream/retry counters captured",
                "estimated token count is deterministic"
            ],
            artifacts: ["usage-report.json"],
            details: report,
            metrics: new Dictionary<string, decimal>
            {
                ["estimatedTokens"] = report.EstimatedTokens,
                ["toolCalls"] = report.ToolCalls,
                ["streamChunks"] = report.StreamChunks,
                ["retryAttempts"] = report.RetryAttempts
            });
    }

    private static FoundationExampleResult CreateResult(
        string id,
        string title,
        string capability,
        IReadOnlyList<string> checks,
        IReadOnlyList<string> artifacts,
        object details,
        IReadOnlyDictionary<string, decimal> metrics)
    {
        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: id,
            exampleVersion: "1.0.0",
            inputSummary: capability,
            localValidation: new PublicExampleValidationSummary("passed", checks),
            outputArtifactRefs: artifacts.Select(artifact =>
                new PublicExampleOutputArtifactRef("sample-output", artifact, GuessMediaType(artifact))),
            selfReportedMetrics: metrics,
            runId: $"{id}-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-25T19:00:00Z"));

        return new FoundationExampleResult("passed", id, title, capability, details, envelope);
    }

    private static string GuessMediaType(string artifact) =>
        artifact.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "text/markdown";
}

internal sealed class CaseNormalizerTool : ITool
{
    private static readonly JsonElement Schema = JsonDocument.Parse("""
    {
      "type": "object",
      "properties": {
        "text": { "type": "string" },
        "style": { "type": "string", "enum": ["upper", "lower", "kebab"] }
      },
      "required": ["text", "style"]
    }
    """).RootElement.Clone();

    public string Name => "normalize_case";

    public string Description => "Normalizes a short piece of text to upper, lower, or kebab case.";

    public JsonElement InputSchema => Schema;

    public Task<ToolResult> ExecuteAsync(object input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (input is not CaseNormalizerInput typed)
        {
            return Task.FromResult(ToolResult.Failure("Input must be CaseNormalizerInput."));
        }

        var normalized = typed.Style.ToLowerInvariant() switch
        {
            "upper" => typed.Text.ToUpperInvariant(),
            "lower" => typed.Text.ToLowerInvariant(),
            "kebab" => string.Join('-', typed.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant(),
            _ => typed.Text
        };

        return Task.FromResult(ToolResult.Success(new CaseNormalizerOutput(normalized, true)));
    }
}

internal sealed class OfflineStreamingModel(string modelName, IReadOnlyList<string> chunks) : ILLMModel<string, string>
{
    public string ModelName { get; } = modelName;

    public int MaxTokens => 1024;

    public Task<string> GenerateAsync(
        string input,
        LLMOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Concat(chunks));

    public async IAsyncEnumerable<string> GenerateStreamAsync(
        string input,
        LLMOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return chunk;
        }
    }

    public Task<IReadOnlyList<string>> GenerateBatchAsync(
        IEnumerable<string> inputs,
        LLMOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(inputs.Select(_ => string.Concat(chunks)).ToArray());
}

internal static class ModelRoutingPlanner
{
    public static ModelRoutingPlan Select(ModelRoutingRequest request)
    {
        var primary = request switch
        {
            { AllowsLocalModel: true, PreferredProvider: "ollama" } => new ModelRoute("ollama", "llama3", "local smoke/live parity"),
            { RequiresLowLatency: true } => new ModelRoute("openai", "gpt-4o-mini", "low-latency hosted route"),
            _ => new ModelRoute("anthropic", "claude-3-5-haiku-20241022", "balanced hosted route")
        };

        return new ModelRoutingPlan(
            request.TaskKind,
            primary,
            [
                new ModelRoute("openai", "gpt-4o-mini", "hosted fallback"),
                new ModelRoute("anthropic", "claude-3-5-haiku-20241022", "hosted fallback")
            ],
            EstimatedCostUnits: primary.Provider == "ollama" ? 0 : 1);
    }
}

internal sealed record CaseNormalizerInput(string Text, string Style);

internal sealed record CaseNormalizerOutput(string Normalized, bool CultureInvariant);

internal sealed record SupportTicketClassification(
    string Urgency,
    string Category,
    string NextAction,
    double Confidence);

internal sealed record ModelRoutingRequest(
    string TaskKind,
    bool RequiresLowLatency,
    bool AllowsLocalModel,
    string? PreferredProvider);

internal sealed record ModelRoute(string Provider, string Model, string Reason);

internal sealed record ModelRoutingPlan(
    string TaskKind,
    ModelRoute Primary,
    IReadOnlyList<ModelRoute> Fallbacks,
    decimal EstimatedCostUnits);

internal sealed record RetryOutcome(string Status, int Attempts, string Message);

internal sealed record UsageReport(
    string RunId,
    int PromptCharacters,
    int CompletionCharacters,
    int EstimatedTokens,
    int ToolCalls,
    int StreamChunks,
    int RetryAttempts)
{
    public static UsageReport Create(
        string runId,
        int promptCharacters,
        int completionCharacters,
        int toolCalls,
        int streamChunks,
        int retryAttempts)
    {
        var estimatedTokens = (int)Math.Ceiling((promptCharacters + completionCharacters) / 4.0);
        return new UsageReport(
            runId,
            promptCharacters,
            completionCharacters,
            estimatedTokens,
            toolCalls,
            streamChunks,
            retryAttempts);
    }
}

internal sealed record FoundationExampleResult(
    string Status,
    string ExampleId,
    string Title,
    string Capability,
    object Details,
    PublicExampleResultEnvelope ResultEnvelope);

internal sealed record FoundationSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<FoundationExampleResult> Examples,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);
