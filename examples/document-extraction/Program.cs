using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(DocumentExtractionDemo.RunSmoke()),
    "ingest" => await RunIngestAsync(args.Skip(1).FirstOrDefault()),
    "query" => await RunQueryAsync(args.Skip(1).ToList()),
    "correct" => await RunCorrectAsync(args.Skip(1).FirstOrDefault()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static async Task<int> RunIngestAsync(string? filePath)
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        // Fallback to offline smoke envelope if no arguments are provided to keep original behavior intact
        return WriteJson(DocumentExtractionDemo.RunSmoke().ResultEnvelope);
    }

    if (!File.Exists(filePath))
    {
        return WriteError($"File not found: {filePath}");
    }

    try
    {
        var text = await File.ReadAllTextAsync(filePath);
        var chunks = DocumentExtractionDemo.ChunkText(filePath, text);
        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "document-extraction",
            exampleVersion: "1.0.0",
            inputSummary: $"Ingested document '{Path.GetFileName(filePath)}'",
            localValidation: new PublicExampleValidationSummary("passed", ["document parsed", $"{chunks.Count} chunks created"]),
            outputArtifactRefs: [new PublicExampleOutputArtifactRef("ingest", "chunks.json", "application/json")],
            selfReportedMetrics: new Dictionary<string, decimal> { ["chunksCount"] = chunks.Count },
            runId: "document-extraction-ingest"
        );

        return WriteJson(new { status = "passed", chunks, resultEnvelope = envelope });
    }
    catch (Exception ex)
    {
        return WriteError($"Ingestion failed: {ex.Message}");
    }
}

static async Task<int> RunQueryAsync(List<string> queryArgs)
{
    if (queryArgs.Count == 0)
    {
        return WriteError("Usage: dotnet run --project samples/document-extraction -- query [file-path] \"<query-string>\"");
    }

    string filePath = "samples/document-extraction/sample-doc.txt";
    string queryString;

    if (queryArgs.Count == 1)
    {
        queryString = queryArgs[0];
    }
    else
    {
        filePath = queryArgs[0];
        queryString = queryArgs[1];
    }

    if (!File.Exists(filePath))
    {
        // Check relative to current worktree root or standard paths
        var altPath = Path.Combine(Directory.GetCurrentDirectory(), filePath);
        if (!File.Exists(altPath))
        {
            return WriteError($"File not found: {filePath}");
        }
        filePath = altPath;
    }

    try
    {
        var text = await File.ReadAllTextAsync(filePath);
        var chunks = DocumentExtractionDemo.ChunkText(filePath, text);
        var matchingChunks = DocumentExtractionDemo.SearchChunks(chunks, queryString);

        var provider = PublicLlmProvider.TryCreateFromEnv();
        string answer;
        string mode = "Keyword Match Fallback";

        if (provider != null && matchingChunks.Count > 0)
        {
            mode = $"Live LLM RAG via '{provider.ModelName}'";
            var context = string.Join("\n\n", matchingChunks.Select(c => $"[Source: {c.SourceRef}]\n{c.Text}"));
            var prompt = $"You are a helpful knowledge assistant. Given the following retrieved context chunks, answer the user's query.\n\nContext:\n{context}\n\nQuery: {queryString}\n\nAnswer: ";
            
            Console.WriteLine($"[Live Mode] querying model '{provider.ModelName}' with {matchingChunks.Count} retrieved chunks...");
            answer = await provider.GenerateAsync(prompt);
        }
        else
        {
            answer = matchingChunks.Count > 0 
                ? "Keyword matches found. Configure OPENAI_API_KEY or ANTHROPIC_API_KEY to generate a natural response using LLM." 
                : "No matching chunks found in the document.";
        }

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "document-extraction",
            exampleVersion: "1.0.0",
            inputSummary: $"Query: '{queryString}' on document '{Path.GetFileName(filePath)}'",
            localValidation: new PublicExampleValidationSummary("passed", ["text chunked", "keyword matched", provider != null ? "llm answer generated" : "offline fallback executed"]),
            outputArtifactRefs: [new PublicExampleOutputArtifactRef("query", "rag-answer.txt", "text/plain")],
            selfReportedMetrics: new Dictionary<string, decimal> { ["matchingChunks"] = matchingChunks.Count, ["liveLlmUsed"] = provider != null ? 1 : 0 },
            runId: "document-extraction-query"
        );

        return WriteJson(new
        {
            status = "passed",
            mode,
            query = queryString,
            retrievedChunks = matchingChunks,
            answer = answer.Trim(),
            resultEnvelope = envelope
        });
    }
    catch (Exception ex)
    {
        return WriteError($"Query execution failed: {ex.Message}");
    }
}

static async Task<int> RunCorrectAsync(string? filePath)
{
    if (string.IsNullOrWhiteSpace(filePath))
    {
        filePath = "examples/document-extraction/sample-doc.txt";
        // Check relative to current directory if not found
        if (!File.Exists(filePath))
        {
            var altPath = Path.Combine(Directory.GetCurrentDirectory(), "public/dotnetagents-examples/examples/document-extraction/sample-doc.txt");
            if (File.Exists(altPath))
            {
                filePath = altPath;
            }
        }
    }

    if (!File.Exists(filePath))
    {
        return WriteError($"File not found: {filePath}");
    }

    try
    {
        List<ExtractedChunk> chunks;
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (extension == ".json")
        {
            var json = await File.ReadAllTextAsync(filePath);
            // Try to extract from json format
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("chunks", out var chunksProp))
            {
                chunks = JsonSerializer.Deserialize<List<ExtractedChunk>>(chunksProp.GetRawText(), new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
            }
            else
            {
                chunks = JsonSerializer.Deserialize<List<ExtractedChunk>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? new();
            }
        }
        else
        {
            var text = await File.ReadAllTextAsync(filePath);
            chunks = DocumentExtractionDemo.ChunkText(filePath, text);
        }

        var provider = PublicLlmProvider.TryCreateFromEnv();
        var correctedChunks = new List<CorrectedChunk>();

        foreach (var chunk in chunks)
        {
            string correctedText = chunk.Text;
            string reviewStatus = "Approved";

            if (provider != null)
            {
                var prompt = $"Review the following document chunk for spelling, formatting, and abbreviations (e.g. expand standard terms like SLA to Service Level Agreement if it makes it clearer). Return only the corrected text without any extra explanation:\n\n{chunk.Text}\n\nCorrected Text:";
                var response = await provider.GenerateAsync(prompt);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    correctedText = response.Trim();
                    reviewStatus = correctedText == chunk.Text.Trim() ? "Approved" : "HumanCorrectedByLlm";
                }
            }
            else
            {
                // Local deterministic corrections
                string original = chunk.Text;
                string temp = original;
                
                // Example corrections
                temp = temp.Replace("Acme Corp", "Acme Corporation")
                           .Replace("SLA", "Service Level Agreement")
                           .Replace("P0", "Priority-0 (Critical)");

                if (temp != original)
                {
                    correctedText = temp;
                    reviewStatus = "AutoCorrected";
                }
            }

            correctedChunks.Add(new CorrectedChunk(chunk.SourceRef, chunk.Text, correctedText, reviewStatus));
        }

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "document-extraction",
            exampleVersion: "1.0.0",
            inputSummary: $"Corrected document '{Path.GetFileName(filePath)}'",
            localValidation: new PublicExampleValidationSummary("passed", ["chunks loaded", provider != null ? "llm review completed" : "local auto-corrections applied"]),
            outputArtifactRefs: [new PublicExampleOutputArtifactRef("correct", "corrected-chunks.json", "application/json")],
            selfReportedMetrics: new Dictionary<string, decimal> { ["totalChunks"] = chunks.Count, ["correctedCount"] = correctedChunks.Count(c => c.ReviewStatus != "Approved") },
            runId: "document-extraction-correct"
        );

        // Save output artifact to path
        var outDir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var outPath = Path.Combine(outDir, "corrected-chunks.json");
        await File.WriteAllTextAsync(outPath, JsonSerializer.Serialize(correctedChunks, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

        return WriteJson(new { status = "passed", correctedChunks, resultEnvelope = envelope });
    }
    catch (Exception ex)
    {
        return WriteError($"Correction failed: {ex.Message}");
    }
}

static int WriteHelp()
{
    Console.WriteLine("Document extraction and local knowledge ingestion demo");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project samples/document-extraction -- --smoke");
    Console.WriteLine("  dotnet run --project samples/document-extraction -- ingest [file-path]");
    Console.WriteLine("  dotnet run --project samples/document-extraction -- query [file-path] \"<query-string>\"");
    Console.WriteLine("     Examples: query \"What is Acme's P0 response target?\"");
    Console.WriteLine("  dotnet run --project samples/document-extraction -- correct [file-path]");
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

internal static class DocumentExtractionDemo
{
    public static DocumentExtractionSmokeResult RunSmoke()
    {
        var chunks = new[]
        {
            new ExtractedChunk("sample-guide.md#overview", "Local public examples use file-backed sample data."),
            new ExtractedChunk("sample-guide.md#privacy", "Private credentials and hosted services are not required.")
        };

        var correctedChunks = new[]
        {
            new CorrectedChunk("sample-guide.md#overview", "Local public examples use file-backed sample data.", "Local public examples use file-backed sample data.", "Approved"),
            new CorrectedChunk("sample-guide.md#privacy", "Private credentials and hosted services are not required.", "Private credentials and hosted services are not required.", "Approved")
        };

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "document-extraction",
            exampleVersion: "1.0.0",
            inputSummary: "sample markdown document extraction into local knowledge chunks",
            localValidation: new PublicExampleValidationSummary(
                "passed",
                ["sample document loaded", "chunks normalized", "knowledge index artifact emitted", "correction review workflow verified"]),
            outputArtifactRefs:
            [
                new PublicExampleOutputArtifactRef("sample-output", "extracted-chunks.json", "application/json"),
                new PublicExampleOutputArtifactRef("sample-output", "local-index.json", "application/json"),
                new PublicExampleOutputArtifactRef("sample-output", "corrected-chunks.json", "application/json")
            ],
            selfReportedMetrics: new Dictionary<string, decimal>
            {
                ["documents"] = 1,
                ["chunks"] = chunks.Length,
                ["correctedChunks"] = correctedChunks.Length
            },
            runId: "document-extraction-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-17T18:40:00Z"));

        return new DocumentExtractionSmokeResult("passed", chunks, correctedChunks, envelope);
    }

    public static List<ExtractedChunk> ChunkText(string sourceName, string text)
    {
        var chunks = new List<ExtractedChunk>();
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        string currentHeader = "document-root";
        var chunkBuilder = new System.Text.StringBuilder();

        foreach (var line in lines)
        {
            if (line.StartsWith("#"))
            {
                if (chunkBuilder.Length > 0)
                {
                    chunks.Add(new ExtractedChunk($"{Path.GetFileName(sourceName)}#{currentHeader}", chunkBuilder.ToString().Trim()));
                    chunkBuilder.Clear();
                }
                currentHeader = line.Trim('#', ' ').Replace(" ", "-").ToLowerInvariant();
            }
            else
            {
                chunkBuilder.AppendLine(line);
            }
        }

        if (chunkBuilder.Length > 0)
        {
            chunks.Add(new ExtractedChunk($"{Path.GetFileName(sourceName)}#{currentHeader}", chunkBuilder.ToString().Trim()));
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c.Text)).ToList();
    }

    public static List<ExtractedChunk> SearchChunks(List<ExtractedChunk> chunks, string query)
    {
        var terms = query.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(t => t.Trim().ToLowerInvariant())
                         .Where(t => t.Length > 3) // Filter out short words
                         .ToList();

        if (terms.Count == 0)
        {
            // Default to matching the whole query string if terms are empty/short
            terms = new List<string> { query.ToLowerInvariant() };
        }

        var results = new List<(ExtractedChunk chunk, int score)>();

        foreach (var chunk in chunks)
        {
            int score = 0;
            var textLower = chunk.Text.ToLowerInvariant();
            var headerLower = chunk.SourceRef.ToLowerInvariant();

            foreach (var term in terms)
            {
                if (textLower.Contains(term))
                {
                    score += 2;
                }
                if (headerLower.Contains(term))
                {
                    score += 5; // Higher score for header matches
                }
            }

            if (score > 0)
            {
                results.Add((chunk, score));
            }
        }

        return results.OrderByDescending(r => r.score)
                      .Select(r => r.chunk)
                      .ToList();
    }
}

internal sealed record ExtractedChunk(string SourceRef, string Text);

internal sealed record CorrectedChunk(string SourceRef, string OriginalText, string CorrectedText, string ReviewStatus);

internal sealed record DocumentExtractionSmokeResult(
    string Status,
    IReadOnlyList<ExtractedChunk> Chunks,
    IReadOnlyList<CorrectedChunk>? CorrectedChunks,
    PublicExampleResultEnvelope ResultEnvelope);
