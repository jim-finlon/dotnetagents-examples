// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DotNetAgents.Core.PublicExamples;

namespace DotNetAgents.Examples.PluginShowcase;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var command = args.Length == 0 ? "--help" : args[0];

        return command switch
        {
            "--smoke" => await RunSmokeAsync(),
            "run" => await RunFamilyAsync(args.Skip(1).FirstOrDefault()),
            "--help" or "-h" => WriteHelp(),
            _ => WriteError($"Unknown command '{command}'. Use --help for usage.")
        };
    }

    private static async Task<int> RunSmokeAsync()
    {
        Console.WriteLine("[Smoke Mode] Initializing and validating all 7 plugin families...");

        // 1. Vector Store Showcase
        var vectorStore = new MockVectorStoreAdapter();
        await vectorStore.IndexAsync(new SearchHit("doc-1", "DotNetAgents implements enterprise security governance.", 0.95, "policy.md"));
        var vectorHits = await vectorStore.SearchAsync(new SearchRequest("security governance"));
        var vectorPassed = vectorHits.Count > 0 && vectorHits[0].Id == "doc-1";

        // 2. Messaging Showcase
        var messaging = new MockMessagingPublisher();
        var message = new AgentStatusUpdate("showcase-agent", "RunningSmokeTests", DateTimeOffset.UtcNow);
        await messaging.PublishAsync("agent-status", message);
        var messagingPassed = messaging.PublishedMessages.Count > 0 && messaging.PublishedMessages[0].Topic == "agent-status";

        // 3. Storage Showcase
        var storage = new MockArtifactStore();
        await storage.SaveAsync("artifact-42", "{\"status\": \"active\"}");
        var savedContent = await storage.LoadAsync("artifact-42");
        var storagePassed = savedContent != null && savedContent.Contains("active");

        // 4. Database Showcase
        var db = new MockDatabaseQueryExecutor();
        var dbRows = await db.ExecuteQueryAsync("SELECT * FROM agents WHERE status = 'active'");
        var dbPassed = dbRows.Count > 0 && dbRows[0].Contains("Agent: 1");

        // 5. Browser Showcase
        var browser = new MockBrowserDriver();
        var pageText = await browser.NavigateAndCaptureAsync("https://dotnetagents.local/docs");
        var browserPassed = pageText.Contains("DotNetAgents");

        // 6. UI Approval Showcase
        var uiApproval = new MockUiApprovalService(autoApprove: true);
        var uiPassed = await uiApproval.RequestApprovalAsync("DeployAgent", "Deploying new production release");

        // 7. Multimodal Showcase
        var multimodal = new MockMultimodalProcessor();
        var imageAnalysis = await multimodal.AnalyzeImageAsync([0x89, 0x50, 0x4E, 0x47], "Describe this logo");
        var multimodalPassed = imageAnalysis.Contains("DotNetAgents Logo");

        var passed = vectorPassed && messagingPassed && storagePassed && dbPassed && browserPassed && uiPassed && multimodalPassed;

        var validation = new PublicExampleValidationSummary(
            passed ? "passed" : "failed",
            new List<string>
            {
                "Vector store index and query succeeded",
                "Messaging publisher enqueued event successfully",
                "Storage adapter saved and loaded artifact",
                "Database executor returned active agents",
                "Browser driver captured page content",
                "UI approval service granted permission",
                "Multimodal processor analyzed image payload"
            }
        );

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "plugin-showcase",
            exampleVersion: "1.0.0",
            inputSummary: "plugin-showcase --smoke",
            localValidation: validation,
            outputArtifactRefs: new List<PublicExampleOutputArtifactRef>
            {
                new("stdout", "console", "application/json")
            },
            selfReportedMetrics: new Dictionary<string, decimal>
            {
                ["pluginsTested"] = 7,
                ["checksPassed"] = passed ? 7 : 0
            },
            runId: "plugin-showcase-smoke",
            timestampUtc: DateTimeOffset.UtcNow
        );

        return WriteJson(new
        {
            status = passed ? "passed" : "failed",
            results = new
            {
                vector = vectorPassed ? "OK" : "Failed",
                messaging = messagingPassed ? "OK" : "Failed",
                storage = storagePassed ? "OK" : "Failed",
                database = dbPassed ? "OK" : "Failed",
                browser = browserPassed ? "OK" : "Failed",
                ui = uiPassed ? "OK" : "Failed",
                multimodal = multimodalPassed ? "OK" : "Failed"
            },
            resultEnvelope = envelope
        }, passed ? 0 : 1);
    }

    private static async Task<int> RunFamilyAsync(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return WriteError("Must specify a family to run. E.g., 'run vector', 'run messaging'");
        }

        switch (family.ToLowerInvariant())
        {
            case "vector":
                var vectorStore = new MockVectorStoreAdapter();
                Console.WriteLine("[Vector Store Demo] Indexing mock policy documents...");
                await vectorStore.IndexAsync(new SearchHit("doc-security", "DotNetAgents security architecture uses signed credentials.", 1.0, "security.md"));
                await vectorStore.IndexAsync(new SearchHit("doc-telemetry", "OpenTelemetry traces and metrics are enabled by default.", 1.0, "telemetry.md"));
                
                var query = "security credentials";
                Console.WriteLine($"Searching for: '{query}'");
                var hits = await vectorStore.SearchAsync(new SearchRequest(query));
                foreach (var hit in hits)
                {
                    Console.WriteLine($"Hit: [{hit.Id}] (Score: {hit.Score:F2}) from {hit.SourceRef} - '{hit.Text}'");
                }
                break;

            case "messaging":
                var messaging = new MockMessagingPublisher();
                Console.WriteLine("[Messaging Demo] Publishing agent state update...");
                await messaging.PublishAsync("agent-status-updates", new AgentStatusUpdate("agent-007", "ActiveExecution", DateTimeOffset.UtcNow));
                Console.WriteLine("Published event to topic: agent-status-updates");
                break;

            case "storage":
                var storage = new MockArtifactStore();
                Console.WriteLine("[Storage Demo] Saving structured task payload...");
                await storage.SaveAsync("task-run-info", "Task outcome: passed. Build SHA: f949504c");
                var content = await storage.LoadAsync("task-run-info");
                Console.WriteLine($"Loaded: {content}");
                break;

            case "database":
                var db = new MockDatabaseQueryExecutor();
                Console.WriteLine("[Database Demo] Executing agent state count query...");
                var rows = await db.ExecuteQueryAsync("SELECT count(*) FROM agent_runs WHERE status = 'failed'");
                foreach (var row in rows)
                {
                    Console.WriteLine($"Row: {row}");
                }
                break;

            case "browser":
                var browser = new MockBrowserDriver();
                Console.WriteLine("[Browser Demo] Navigating to local-first sandbox page...");
                var html = await browser.NavigateAndCaptureAsync("https://agent-sandbox.local/login");
                Console.WriteLine($"Captured text:\n{html}");
                break;

            case "ui":
                var ui = new MockUiApprovalService(autoApprove: false);
                Console.WriteLine("[UI Approval Demo] Prompting operator for mutating execution clearance...");
                var approved = await ui.RequestApprovalAsync("DeleteCache", "Clearing temporary build artifacts");
                Console.WriteLine($"Approval result: {(approved ? "GRANTED" : "DENIED")}");
                break;

            case "multimodal":
                var multimodal = new MockMultimodalProcessor();
                Console.WriteLine("[Multimodal Demo] Sending dummy image bytes for analysis...");
                var analysis = await multimodal.AnalyzeImageAsync([0xFF, 0xD8, 0xFF], "Identify object in image");
                Console.WriteLine($"Analysis result: {analysis}");
                break;

            default:
                return WriteError($"Unknown plugin family '{family}'. Available: vector, messaging, storage, database, browser, ui, multimodal");
        }

        return 0;
    }

    private static int WriteHelp()
    {
        Console.WriteLine("DotNetAgents Plugin Showcase Demo");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project examples/plugin-showcase -- --smoke");
        Console.WriteLine("  dotnet run --project examples/plugin-showcase -- run <family>");
        Console.WriteLine();
        Console.WriteLine("Families:");
        Console.WriteLine("  vector       Showcases semantic vector index & query");
        Console.WriteLine("  messaging    Showcases publisher-subscriber events");
        Console.WriteLine("  storage      Showcases artifact load/store actions");
        Console.WriteLine("  database     Showcases safe read-only SQL query runs");
        Console.WriteLine("  browser      Showcases sandbox browser/page inspection");
        Console.WriteLine("  ui           Showcases interactive operator approval");
        Console.WriteLine("  multimodal   Showcases media processing & image description");
        return 0;
    }

    private static int WriteError(string message)
    {
        Console.Error.WriteLine($"[Error] {message}");
        return 2;
    }

    private static int WriteJson<T>(T value, int exitCode = 0)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, PublicExampleResultEnvelopeJson.SerializerOptions));
        return exitCode;
    }
}

#region Interfaces and Mock Adapters

// 1. Vector Store Abstractions
public interface IVectorStoreAdapter
{
    Task IndexAsync(SearchHit hit, CancellationToken ct = default);
    Task<IReadOnlyList<SearchHit>> SearchAsync(SearchRequest request, CancellationToken ct = default);
}

public sealed record SearchRequest(string Query, int TopK = 3);
public sealed record SearchHit(string Id, string Text, double Score, string SourceRef);

public sealed class MockVectorStoreAdapter : IVectorStoreAdapter
{
    private readonly List<SearchHit> _store = new();

    public Task IndexAsync(SearchHit hit, CancellationToken ct = default)
    {
        _store.Add(hit);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchHit>> SearchAsync(SearchRequest request, CancellationToken ct = default)
    {
        var hits = _store
            .Where(h => h.Text.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ||
                        request.Query.Split(' ').Any(q => h.Text.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Take(request.TopK)
            .ToList();
        return Task.FromResult<IReadOnlyList<SearchHit>>(hits);
    }
}

// 2. Messaging Abstractions
public interface IMessagingPublisher
{
    Task PublishAsync<T>(string topic, T message, CancellationToken ct = default);
}

public sealed record AgentStatusUpdate(string AgentId, string Status, DateTimeOffset Timestamp);
public sealed record PublishedEnvelope(string Topic, object Payload);

public sealed class MockMessagingPublisher : IMessagingPublisher
{
    public List<PublishedEnvelope> PublishedMessages { get; } = new();

    public Task PublishAsync<T>(string topic, T message, CancellationToken ct = default)
    {
        PublishedMessages.Add(new PublishedEnvelope(topic, message!));
        return Task.CompletedTask;
    }
}

// 3. Storage Abstractions
public interface IArtifactStore
{
    Task SaveAsync(string key, string content, CancellationToken ct = default);
    Task<string?> LoadAsync(string key, CancellationToken ct = default);
}

public sealed class MockArtifactStore : IArtifactStore
{
    private readonly Dictionary<string, string> _memoryStore = new();

    public Task SaveAsync(string key, string content, CancellationToken ct = default)
    {
        _memoryStore[key] = content;
        return Task.CompletedTask;
    }

    public Task<string?> LoadAsync(string key, CancellationToken ct = default)
    {
        _memoryStore.TryGetValue(key, out var content);
        return Task.FromResult(content);
    }
}

// 4. Database Abstractions
public interface IDatabaseQueryExecutor
{
    Task<IReadOnlyList<string>> ExecuteQueryAsync(string query, CancellationToken ct = default);
}

public sealed class MockDatabaseQueryExecutor : IDatabaseQueryExecutor
{
    public Task<IReadOnlyList<string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        if (query.Contains("SELECT count", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Count: 3" });
        }
        return Task.FromResult<IReadOnlyList<string>>(new List<string> { "Agent: 1 | Name: public-demo-agent | Status: active" });
    }
}

// 5. Browser Abstractions
public interface IBrowserDriver
{
    Task<string> NavigateAndCaptureAsync(string url, CancellationToken ct = default);
}

public sealed class MockBrowserDriver : IBrowserDriver
{
    public Task<string> NavigateAndCaptureAsync(string url, CancellationToken ct = default)
    {
        return Task.FromResult($"[Browser] Successfully loaded URL: {url}\nPage Content: DotNetAgents Web App Shell loaded.");
    }
}

// 6. UI Approval Abstractions
public interface IUiApprovalService
{
    Task<bool> RequestApprovalAsync(string actionName, string reason, CancellationToken ct = default);
}

public sealed class MockUiApprovalService : IUiApprovalService
{
    private readonly bool _autoApprove;

    public MockUiApprovalService(bool autoApprove)
    {
        _autoApprove = autoApprove;
    }

    public Task<bool> RequestApprovalAsync(string actionName, string reason, CancellationToken ct = default)
    {
        if (_autoApprove)
        {
            return Task.FromResult(true);
        }
        Console.Write($"[Operator Approval Required] Approve action '{actionName}' for '{reason}'? (y/n): ");
        var input = Console.ReadLine();
        return Task.FromResult(string.Equals(input?.Trim(), "y", StringComparison.OrdinalIgnoreCase));
    }
}

// 7. Multimodal Abstractions
public interface IMultimodalProcessor
{
    Task<string> AnalyzeImageAsync(byte[] imageBytes, string prompt, CancellationToken ct = default);
}

public sealed class MockMultimodalProcessor : IMultimodalProcessor
{
    public Task<string> AnalyzeImageAsync(byte[] imageBytes, string prompt, CancellationToken ct = default)
    {
        return Task.FromResult($"[Multimodal Analysis] Analyzed {imageBytes.Length} bytes using prompt '{prompt}'. Result: DotNetAgents Logo containing the enterprise shield.");
    }
}

#endregion
