using DotNetAgents.Mcp.Models;
using DotNetAgents.Mcp.Server;

namespace Dna.McpThinHost.Template;

public sealed class SampleMcpToolProvider : IMcpToolProvider
{
    private const string ServiceName = "sample_thin_mcp";
    private const string DirectoryLink = "https://github.com/jim-finlon/dotnetagents-examples/blob/main/docs/walkthroughs/mcp-thin-host.md";

    private readonly SampleDomainStore _store;

    public SampleMcpToolProvider(SampleDomainStore store)
    {
        _store = store;
    }

    public static McpInstructionsResponse GetInstructionsBootstrap()
        => new()
        {
            ServiceName = ServiceName,
            Description = "Sample DNA thin MCP host showing bootstrap, tools/list, tools/call, health, streamable MCP, and optional shared seams.",
            BootstrapStep = "Call GET /mcp/tools, then POST /mcp/tools/call with tool get_instructions or echo.",
            BaseUrlNote = "Use the same host as this request for all /mcp/* paths.",
            Consumers = "Cursor, Claude, Codex, JARVIS, or operators with explicit service access.",
            DirectoryLink = DirectoryLink,
            ConfigSnippets = new Dictionary<string, string>
            {
                ["cursor"] = "HTTP MCP url: http://localhost:5080",
                ["jarvis"] = "{ \"ServiceName\": \"sample_thin_mcp\", \"BaseUrl\": \"http://sample-thin-mcp:5080\" }"
            }
        };

    public Task<IReadOnlyList<McpToolDefinition>> GetToolsAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<McpToolDefinition> tools =
        [
            Tool(serviceName, "get_instructions", "Return service bootstrap and consumer hints.", "sample.meta", []),
            Tool(serviceName, "echo", "Echo an input value to prove request/response wiring.", "sample.diagnostics", ["message"]),
            Tool(serviceName, "get_sample_status", "Return a tiny read model proving service dependency wiring.", "sample.status", [])
        ];

        return Task.FromResult(tools);
    }

    public Task<McpToolCallResponse> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        var response = toolName switch
        {
            "get_instructions" => new McpToolCallResponse
            {
                Success = true,
                Result = GetInstructionsBootstrap(),
                Summary = "Sample thin MCP bootstrap returned.",
                Guidance = "Call echo or get_sample_status next.",
                SuggestedNextSteps = ["echo", "get_sample_status"]
            },
            "echo" => Echo(arguments),
            "get_sample_status" => new McpToolCallResponse
            {
                Success = true,
                Result = _store.GetStatus(),
                Summary = "Sample thin MCP status returned.",
                SuggestedNextSteps = ["get_instructions"]
            },
            _ => new McpToolCallResponse
            {
                Success = false,
                Error = $"Unknown tool '{toolName}'",
                ErrorCode = "NOT_FOUND",
                Guidance = "Call get_instructions or GET /mcp/tools."
            }
        };

        return Task.FromResult(response);
    }

    private static McpToolCallResponse Echo(IReadOnlyDictionary<string, object> arguments)
    {
        if (!arguments.TryGetValue("message", out var raw) || string.IsNullOrWhiteSpace(raw?.ToString()))
        {
            return new McpToolCallResponse
            {
                Success = false,
                Error = "message is required",
                ErrorCode = "MISSING_ARG"
            };
        }

        return new McpToolCallResponse
        {
            Success = true,
            Result = new { message = raw.ToString() },
            Summary = "Echo completed."
        };
    }

    private static McpToolDefinition Tool(
        string serviceName,
        string name,
        string description,
        string category,
        IReadOnlyList<string> requiredArguments)
        => new()
        {
            ServiceName = serviceName,
            Name = name,
            Description = description,
            Category = category,
            InputSchema = new McpToolInputSchema
            {
                Required = requiredArguments.ToList(),
                Properties = requiredArguments.ToDictionary(
                    argument => argument,
                    argument => new McpProperty
                    {
                        Type = "string",
                        Description = $"Required {argument} argument."
                    },
                    StringComparer.Ordinal)
            },
            Examples = requiredArguments.Count == 0
                ? [$"{name}()"]
                : [$"{name}({string.Join(", ", requiredArguments.Select(argument => argument + ": value"))})"]
        };
}

public sealed class SampleDomainStore
{
    public object GetStatus()
        => new
        {
            status = "ok",
            source = "SampleDomainStore",
            timestampUtc = DateTimeOffset.UtcNow
        };
}
