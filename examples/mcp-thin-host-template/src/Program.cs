using DotNetAgents.Contracts;
using DotNetAgents.Mcp;
using DotNetAgents.Mcp.Models;
using DotNetAgents.Mcp.Server;
using Dna.McpThinHost.Template;
using System.Text.Json;

if (args.Contains("--smoke", StringComparer.OrdinalIgnoreCase))
{
    return await ThinMcpTemplateSmoke.RunAsync().ConfigureAwait(false);
}

var builder = WebApplication.CreateBuilder(args);

const string serviceName = "sample_thin_mcp";
const string displayName = "Sample Thin MCP";
const string version = "1.0.0";

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = false;
});

// Register service-specific dependencies here.
builder.Services.AddSingleton<SampleDomainStore>();

// Optional shared seams. Keep them config-gated for services that do not need them.
if (builder.Configuration.GetValue("ThinMcp:Learning:Enabled", false))
{
    builder.Services.AddAgentLearningProjection(options =>
    {
        options.Enabled = true;
        options.TimeoutMs = builder.Configuration.GetValue("ThinMcp:Learning:TimeoutMs", 1200);
    });
}

if (builder.Configuration.GetValue("ThinMcp:GeneticContract:Enabled", false))
{
    builder.Services.AddGeneticContractReader("SampleThinMcp");
}

builder.Services.AddScoped<SampleMcpToolProvider>();
builder.Services.AddScoped<IMcpToolProvider>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var provider = sp.GetRequiredService<SampleMcpToolProvider>();

    if (!configuration.GetValue("ThinMcp:Learning:Enabled", false))
        return provider;

    return new McpLearningDecorator(
        provider,
        sp.GetRequiredService<DotNetAgents.Mcp.Abstractions.IAgentLearningProjector>(),
        sp.GetRequiredService<ILogger<McpLearningDecorator>>(),
        new McpLearningDecoratorOptions
        {
            EventLogPath = configuration["ThinMcp:Learning:EventLogPath"]
                ?? Environment.GetEnvironmentVariable("THIN_MCP_LEARNING_EVENT_LOG")
                ?? Path.Combine(AppContext.BaseDirectory, "data", "learning-events.ndjson"),
            ProjectName = configuration["ThinMcp:Learning:Project"]
                ?? "dna-sample-thin-mcp",
            Service = serviceName,
            ActorId = "sample-thin-mcp-api",
            SuccessConfidence = 0.75,
            FailureConfidence = 0.5
        });
});

var app = builder.Build();

app.UseCursorMcpCors();
app.UseThinMcpApiKeyIfConfigured("ThinMcp:ApiKey", "THIN_MCP_API_KEY");

app.MapGet("/", () => Results.Ok(new { service = serviceName, status = "ok" }));
app.MapGet("/health", () => Results.Ok(new { service = serviceName, status = "healthy" }));

if (app.Configuration.GetValue("ThinMcp:GeneticContract:Enabled", false))
{
    app.MapGet("/genetic/contract", async (GeneticContractReader reader, CancellationToken cancellationToken) =>
    {
        var response = await reader.ReadResponseAsync(cancellationToken).ConfigureAwait(false);
        return Results.Json(new
        {
            contract = response.Contract,
            source = response.Source,
            policySummary = new
            {
                promotionLane = "lab_only",
                previewConfirmRequired = true,
                requiresHumanApproval = true
            }
        });
    });
}

var instructions = SampleMcpToolProvider.GetInstructionsBootstrap();
app.MapMcpEndpoints(serviceName, true, instructions);
app.MapMcpStreamableHttp(serviceName, displayName, version);

app.Run();
return 0;

internal static class ThinMcpTemplateSmoke
{
    public static async Task<int> RunAsync()
    {
        var provider = new SampleMcpToolProvider(new SampleDomainStore());
        var tools = await provider.GetToolsAsync("sample_thin_mcp").ConfigureAwait(false);
        var instructions = await provider.CallToolAsync("get_instructions", new Dictionary<string, object>()).ConfigureAwait(false);
        var echo = await provider.CallToolAsync(
            "echo",
            new Dictionary<string, object> { ["message"] = "hello protocol smoke" }).ConfigureAwait(false);
        var status = await provider.CallToolAsync("get_sample_status", new Dictionary<string, object>()).ConfigureAwait(false);
        var missing = await provider.CallToolAsync("missing_tool", new Dictionary<string, object>()).ConfigureAwait(false);

        var passed = tools.Count == 3 &&
                     tools.All(tool => tool.InputSchema is not null) &&
                     instructions.Success &&
                     echo.Success &&
                     status.Success &&
                     !missing.Success &&
                     missing.ErrorCode == "NOT_FOUND";

        var output = new
        {
            status = passed ? "passed" : "failed",
            service = "sample_thin_mcp",
            toolCount = tools.Count,
            tools = tools.Select(tool => new
            {
                tool.Name,
                tool.Category,
                required = tool.InputSchema.Required
            }),
            calls = new
            {
                instructions = instructions.Success,
                echo = echo.Success,
                sampleStatus = status.Success,
                unknownToolRejected = !missing.Success
            },
            transcript = new[]
            {
                "GET /mcp/instructions",
                "GET /mcp/tools",
                "POST /mcp/tools/call get_instructions",
                "POST /mcp/tools/call echo",
                "POST /mcp/tools/call get_sample_status"
            }
        };

        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        }));
        return passed ? 0 : 1;
    }
}

internal static class ThinMcpAuthExtensions
{
    public static IApplicationBuilder UseThinMcpApiKeyIfConfigured(
        this WebApplication app,
        string configKey,
        string environmentVariable)
    {
        var apiKey = app.Configuration[configKey] ?? Environment.GetEnvironmentVariable(environmentVariable);
        if (string.IsNullOrWhiteSpace(apiKey))
            return app;

        app.Use(async (ctx, next) =>
        {
            if (HttpMethods.IsGet(ctx.Request.Method) &&
                (ctx.Request.Path == "/" ||
                 ctx.Request.Path == "/health" ||
                 ctx.Request.Path == "/mcp/instructions"))
            {
                await next().ConfigureAwait(false);
                return;
            }

            if (!ctx.Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
                !string.Equals(provided.ToString(), apiKey, StringComparison.Ordinal))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await ctx.Response.WriteAsJsonAsync(new { error = "invalid or missing X-Api-Key" }).ConfigureAwait(false);
                return;
            }

            await next().ConfigureAwait(false);
        });

        return app;
    }
}
