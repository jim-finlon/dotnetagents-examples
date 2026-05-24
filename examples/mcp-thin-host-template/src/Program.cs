using DotNetAgents.Contracts;
using DotNetAgents.Mcp;
using DotNetAgents.Mcp.Models;
using DotNetAgents.Mcp.Server;
using Dna.McpThinHost.Template;

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
