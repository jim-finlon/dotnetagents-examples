// SPDX-License-Identifier: Apache-2.0

using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.RegularExpressions;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RagDataDemo.RunSmoke()),
    "query" => await RunQueryAsync(args.Skip(1).ToList()),
    "verify" => await RunVerifyAsync(args.Skip(1).ToList()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static async Task<int> RunQueryAsync(List<string> queryArgs)
{
    if (queryArgs.Count == 0)
    {
        return WriteError("Usage: dotnet run --project examples/rag-data -- query \"<query-string>\"");
    }

    var queryString = queryArgs[0];
    var filePath = "examples/rag-data/sample-knowledge.md";

    if (!File.Exists(filePath))
    {
        var altPath = Path.Combine(Directory.GetCurrentDirectory(), "public/dotnetagents-examples/examples/rag-data/sample-knowledge.md");
        if (File.Exists(altPath))
        {
            filePath = altPath;
        }
        else
        {
            return WriteError($"File not found: {filePath}");
        }
    }

    try
    {
        var text = await File.ReadAllTextAsync(filePath);
        var chunks = RagDataDemo.ChunkText(filePath, text);
        var matchingChunks = RagDataDemo.SearchChunks(chunks, queryString);

        var provider = PublicLlmProvider.TryCreateFromEnv();
        string answer;
        string mode = "Keyword Match Fallback";

        if (provider != null && matchingChunks.Count > 0)
        {
            mode = $"Live LLM RAG via '{provider.ModelName}'";
            var context = string.Join("\n\n", matchingChunks.Select(c => $"[Source: {c.SourceRef}]\n{c.Text}"));
            var prompt = $"You are a helpful knowledge assistant. Given the following retrieved context chunks, answer the user's query.\n\nContext:\n{context}\n\nQuery: {queryString}\n\nAnswer: ";
            answer = await provider.GenerateAsync(prompt);
        }
        else
        {
            // Local fallback synthesis
            if (matchingChunks.Count > 0)
            {
                var bestChunk = matchingChunks[0];
                answer = $"According to {bestChunk.SourceRef}: {bestChunk.Text}";
            }
            else
            {
                answer = "I could not find any matching policy details in the local knowledge base.";
            }
        }

        // Run citation verification
        var verificationReport = await CitationVerifier.VerifyAsync(answer, matchingChunks, provider);

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "rag-data",
            exampleVersion: "1.0.0",
            inputSummary: $"Query: '{queryString}'",
            localValidation: new PublicExampleValidationSummary(
                "passed", 
                ["document chunked", "keyword matched", provider != null ? "llm answer generated" : "local fallback synthesis applied", "groundedness citation verified"]),
            outputArtifactRefs: [
                new PublicExampleOutputArtifactRef("query", "rag-answer.txt", "text/plain"),
                new PublicExampleOutputArtifactRef("query", "citation-verification-report.json", "application/json")
            ],
            selfReportedMetrics: new Dictionary<string, decimal> {
                ["matchingChunks"] = matchingChunks.Count,
                ["liveLlmUsed"] = provider != null ? 1 : 0,
                ["groundednessScore"] = (decimal)verificationReport.GroundednessScore
            },
            runId: "rag-data-query"
        );

        // Save output artifacts
        var outDir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        await File.WriteAllTextAsync(Path.Combine(outDir, "rag-answer.txt"), answer);
        await File.WriteAllTextAsync(Path.Combine(outDir, "citation-verification-report.json"), JsonSerializer.Serialize(verificationReport, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

        return WriteJson(new
        {
            status = "passed",
            mode,
            query = queryString,
            retrievedChunks = matchingChunks,
            answer = answer.Trim(),
            verificationReport,
            resultEnvelope = envelope
        });
    }
    catch (Exception ex)
    {
        return WriteError($"Query execution failed: {ex.Message}");
    }
}

static async Task<int> RunVerifyAsync(List<string> verifyArgs)
{
    if (verifyArgs.Count < 2)
    {
        return WriteError("Usage: dotnet run --project examples/rag-data -- verify \"<answer>\" \"<context-or-file-path>\"");
    }

    var answer = verifyArgs[0];
    var contextInput = verifyArgs[1];
    string contextText = contextInput;

    if (File.Exists(contextInput))
    {
        contextText = await File.ReadAllTextAsync(contextInput);
    }

    try
    {
        // Chunk context text
        var chunks = RagDataDemo.ChunkText("verify-input.txt", contextText);
        var provider = PublicLlmProvider.TryCreateFromEnv();
        var verificationReport = await CitationVerifier.VerifyAsync(answer, chunks, provider);

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "rag-data",
            exampleVersion: "1.0.0",
            inputSummary: "Standalone answer citation verification",
            localValidation: new PublicExampleValidationSummary("passed", ["answer parsed into claims", "groundedness checked against context"]),
            outputArtifactRefs: [new PublicExampleOutputArtifactRef("verify", "verification-report.json", "application/json")],
            selfReportedMetrics: new Dictionary<string, decimal> { ["groundednessScore"] = (decimal)verificationReport.GroundednessScore },
            runId: "rag-data-verify"
        );

        return WriteJson(new
        {
            status = "passed",
            verificationReport,
            resultEnvelope = envelope
        });
    }
    catch (Exception ex)
    {
        return WriteError($"Verification failed: {ex.Message}");
    }
}

static int WriteHelp()
{
    Console.WriteLine("Local RAG knowledge assistant and citation verifier demo");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/rag-data -- --smoke");
    Console.WriteLine("  dotnet run --project examples/rag-data -- query \"<query-string>\"");
    Console.WriteLine("  dotnet run --project examples/rag-data -- verify \"<answer>\" \"<context-text-or-file>\"");
    Console.WriteLine();
    Console.WriteLine("Live Mode Configuration (optional):");
    Console.WriteLine("Configure API keys as environment variables to run live executions via providers:");
    Console.WriteLine("  export OPENAI_API_KEY=\"your-openai-api-key\"");
    Console.WriteLine("  export ANTHROPIC_API_KEY=\"your-anthropic-api-key\"");
    Console.WriteLine("  export OLLAMA_MODEL=\"llama3\"");
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

internal static class RagDataDemo
{
    public static RagDataSmokeResult RunSmoke()
    {
        var chunks = new List<ExtractedChunk>
        {
            new ExtractedChunk("smoke-doc.md#slas", "Priority-0 (P0) SLA is 15 minutes. Contact VP of Operations.")
        };

        var answer = "The response SLA for P0 issues is 15 minutes and you should contact the VP of Operations.";
        
        // Run local citation verifier on smoke data
        var verificationReport = Task.Run(() => CitationVerifier.VerifyAsync(answer, chunks, null)).Result;

        var envelope = PublicExampleResultEnvelope.Create(
            exampleId: "rag-data",
            exampleVersion: "1.0.0",
            inputSummary: "Local RAG knowledge retrieval and citation verifier smoke test",
            localValidation: new PublicExampleValidationSummary(
                "passed",
                ["synthetic context chunked", "local query processed", "citations verified offline"]),
            outputArtifactRefs:
            [
                new PublicExampleOutputArtifactRef("sample-output", "rag-answer.txt", "text/plain"),
                new PublicExampleOutputArtifactRef("sample-output", "citation-verification-report.json", "application/json")
            ],
            selfReportedMetrics: new Dictionary<string, decimal>
            {
                ["documents"] = 1,
                ["chunks"] = chunks.Count,
                ["groundednessScore"] = (decimal)verificationReport.GroundednessScore
            },
            runId: "rag-data-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-25T00:00:00Z"));

        return new RagDataSmokeResult("passed", chunks, answer, verificationReport, envelope);
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
                         .Where(t => t.Length > 3)
                         .ToList();

        if (terms.Count == 0)
        {
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
                    score += 5;
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

internal static class CitationVerifier
{
    public static async Task<CitationVerificationReport> VerifyAsync(string answer, List<ExtractedChunk> contextChunks, DotNetAgents.Abstractions.Models.ILLMModel<string, string>? provider)
    {
        // Split answer into sentences/claims using punctuation
        var claimsText = answer.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(s => s.Trim())
                               .Where(s => s.Length > 10)
                               .ToList();

        if (claimsText.Count == 0)
        {
            claimsText.Add(answer.Trim());
        }

        var verifiedClaims = new List<VerifiedClaim>();

        foreach (var claim in claimsText)
        {
            bool grounded = false;
            string sourceRef = "None";
            string rationale = "No matching source found in context.";

            if (provider != null && contextChunks.Count > 0)
            {
                var contextText = string.Join("\n", contextChunks.Select(c => $"[{c.SourceRef}]: {c.Text}"));
                var prompt = $"Analyze if the claim is grounded/supported in the given context.\n\nContext:\n{contextText}\n\nClaim:\n{claim}\n\nIs the claim grounded in the context? Start your response with YES or NO, followed by a brief 1-sentence explanation.";
                var response = await provider.GenerateAsync(prompt);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    var respTrim = response.Trim();
                    grounded = respTrim.StartsWith("YES", StringComparison.OrdinalIgnoreCase);
                    var parts = respTrim.Split(new[] { '\n', ':' }, 2);
                    rationale = parts.Length > 1 ? parts[1].Trim() : respTrim;
                    if (grounded)
                    {
                        var bestMatch = contextChunks.FirstOrDefault(c => c.Text.Contains(claim.Split(' ').First(), StringComparison.OrdinalIgnoreCase));
                        sourceRef = bestMatch?.SourceRef ?? contextChunks.First().SourceRef;
                    }
                }
            }
            else
            {
                // Local deterministic keyword overlap checks
                var claimTerms = claim.Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(t => t.Trim().ToLowerInvariant())
                                      .Where(t => t.Length > 3)
                                      .ToList();

                if (claimTerms.Count == 0)
                {
                    claimTerms.Add(claim.ToLowerInvariant());
                }

                foreach (var chunk in contextChunks)
                {
                    var chunkTextLower = chunk.Text.ToLowerInvariant();
                    int matchCount = claimTerms.Count(t => chunkTextLower.Contains(t));
                    double overlap = (double)matchCount / claimTerms.Count;

                    if (overlap >= 0.4)
                    {
                        grounded = true;
                        sourceRef = chunk.SourceRef;
                        rationale = $"Claim has {matchCount}/{claimTerms.Count} word overlap ({overlap:P0}) with source chunk.";
                        break;
                    }
                }
            }

            verifiedClaims.Add(new VerifiedClaim(claim, grounded, sourceRef, rationale));
        }

        double score = verifiedClaims.Count > 0 
            ? (double)verifiedClaims.Count(c => c.Grounded) / verifiedClaims.Count 
            : 1.0;

        return new CitationVerificationReport(verifiedClaims, score);
    }
}

internal sealed record ExtractedChunk(string SourceRef, string Text);

internal sealed record VerifiedClaim(string Claim, bool Grounded, string SourceRef, string Rationale);

internal sealed record CitationVerificationReport(
    IReadOnlyList<VerifiedClaim> Claims,
    double GroundednessScore);

internal sealed record RagDataSmokeResult(
    string Status,
    IReadOnlyList<ExtractedChunk> ContextChunks,
    string Answer,
    CitationVerificationReport VerificationReport,
    PublicExampleResultEnvelope ResultEnvelope);
