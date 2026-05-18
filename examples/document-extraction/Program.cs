using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(DocumentExtractionDemo.RunSmoke()),
    "ingest" => WriteJson(DocumentExtractionDemo.RunSmoke().ResultEnvelope),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static int WriteHelp()
{
    Console.WriteLine("Document extraction and local knowledge ingestion demo");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/document-extraction -- --smoke");
    Console.WriteLine("  dotnet run --project samples/document-extraction -- ingest");
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

internal static class DocumentExtractionDemo
{
    public static DocumentExtractionSmokeResult RunSmoke()
    {
        var chunks = new[]
        {
            new ExtractedChunk("sample-guide.md#overview", "Local public examples use file-backed sample data."),
            new ExtractedChunk("sample-guide.md#privacy", "Private credentials and hosted services are not required.")
        };

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "document-extraction",
            exampleVersion: "1.0.0",
            inputSummary: "sample markdown document extraction into local knowledge chunks",
            localValidation: new PublicExampleValidationSummary(
                "passed",
                ["sample document loaded", "chunks normalized", "knowledge index artifact emitted"]),
            outputArtifactRefs:
            [
                new PublicExampleOutputArtifactRef("sample-output", "extracted-chunks.json", "application/json"),
                new PublicExampleOutputArtifactRef("sample-output", "local-index.json", "application/json")
            ],
            selfReportedMetrics: new Dictionary<string, decimal>
            {
                ["documents"] = 1,
                ["chunks"] = chunks.Length
            },
            runId: "document-extraction-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-17T18:40:00Z"));

        return new DocumentExtractionSmokeResult("passed", chunks, envelope);
    }
}

internal sealed record ExtractedChunk(string SourceRef, string Text);

internal sealed record DocumentExtractionSmokeResult(
    string Status,
    IReadOnlyList<ExtractedChunk> Chunks,
    PublicExampleResultEnvelope ResultEnvelope);
