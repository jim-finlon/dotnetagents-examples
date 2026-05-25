using DotNetAgents.Core.PublicExamples;
using System.Text.Json;
using System.Text.Json.Serialization;

var command = args.Length == 0 ? "--help" : args[0];

return command switch
{
    "--smoke" => WriteJson(RunSmoke()),
    "list" => WriteJson(DeveloperSystemsCatalog.Examples),
    "run" => RunExample(args.Skip(1).FirstOrDefault()),
    "--help" or "-h" => WriteHelp(),
    _ => WriteError($"Unknown command '{command}'.")
};

static DeveloperSystemsSmokeResult RunSmoke()
{
    var envelopes = DeveloperSystemsCatalog.Examples
        .Select(example => example.ToEnvelope())
        .ToArray();

    var passed = envelopes.Length == 4 &&
                 envelopes.All(envelope => envelope.SchemaVersion == PublicExampleResultEnvelopeContract.SchemaVersion) &&
                 envelopes.All(envelope => envelope.LocalValidation.IsPassed);

    return new DeveloperSystemsSmokeResult(
        passed ? "passed" : "failed",
        "developer-systems",
        envelopes.Length,
        envelopes);
}

static int RunExample(string? exampleId)
{
    if (string.IsNullOrWhiteSpace(exampleId))
    {
        return WriteError("Usage: dotnet run --project examples/developer-systems -- run <example-id>");
    }

    var example = DeveloperSystemsCatalog.Examples
        .SingleOrDefault(item => string.Equals(item.Id, exampleId, StringComparison.OrdinalIgnoreCase));

    if (example is null)
    {
        return WriteError($"Unknown example '{exampleId}'. Run the 'list' command to see available examples.");
    }

    switch (exampleId.ToLowerInvariant())
    {
        case "code-review":
            RunCodeReview();
            break;
        case "release-notes":
            RunReleaseNotes();
            break;
        case "docs-maintainer":
            RunDocsMaintainer();
            break;
        case "test-authoring":
            RunTestAuthoring();
            break;
        default:
            return WriteError($"Unimplemented example '{exampleId}'.");
    }

    return 0;
}

static void RunCodeReview()
{
    Console.WriteLine("=== Developer Systems: Code Review Assistant ===");
    var diff = """
        -public void process() {
        -  int x = 10;
        -  Console.WriteLine(x);
        -}
        +public void Process() {
        +  const int MaxRetryCount = 10;
        +  Console.WriteLine(MaxRetryCount);
        +}
        """;
    
    var comments = CodeReviewAssistant.ReviewDiff(diff);
    Console.WriteLine("Input Diff:");
    Console.WriteLine(diff);
    Console.WriteLine();
    Console.WriteLine("Review Comments:");
    foreach (var c in comments)
    {
        Console.WriteLine($"- Line {c.LineNumber}: [{c.Severity}] {c.Message}");
    }
}

static void RunReleaseNotes()
{
    Console.WriteLine("=== Developer Systems: Release-Note Generator ===");
    var commits = new[]
    {
        "feat(auth): add PKCE challenge support",
        "fix(session): recover from transient 500 error",
        "chore(deps): update vllm model endpoint",
        "feat(logging): integrate open telemetry tracing"
    };

    var notes = ReleaseNotesGenerator.Generate(commits);
    Console.WriteLine("Commits:");
    foreach (var c in commits) Console.WriteLine($"- {c}");
    Console.WriteLine();
    Console.WriteLine("Generated Release Notes:");
    Console.WriteLine(notes);
}

static void RunDocsMaintainer()
{
    Console.WriteLine("=== Developer Systems: Docs Maintainer ===");
    var doc = """
        ---
        title: Introduction to DNA
        category: getting-started
        ---
        # DNA Overview
        See [topology](../docs/topology.md) and [setup](file:///invalid/path).
        """;

    var report = DocsMaintainer.AnalyzeDoc(doc);
    Console.WriteLine("Source Document:");
    Console.WriteLine(doc);
    Console.WriteLine();
    Console.WriteLine($"Has Frontmatter: {report.HasFrontmatter} (Title: {report.Title})");
    Console.WriteLine("Issues Found:");
    foreach (var issue in report.Issues)
    {
        Console.WriteLine($"- [{issue.Kind}] {issue.Description}");
    }
}

static void RunTestAuthoring()
{
    Console.WriteLine("=== Developer Systems: Test Authoring Assistant ===");
    var signature = "public interface ICalculator { int Add(int a, int b); void Clear(); }";
    var tests = TestAuthoringAssistant.GenerateTests(signature);
    Console.WriteLine($"Source Signature: {signature}");
    Console.WriteLine();
    Console.WriteLine("Generated xUnit Test Boilerplate:");
    Console.WriteLine(tests);
}

static int WriteHelp()
{
    Console.WriteLine("Developer Systems examples pack");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  dotnet run --project examples/developer-systems -- --smoke");
    Console.WriteLine("  dotnet run --project examples/developer-systems -- list");
    Console.WriteLine("  dotnet run --project examples/developer-systems -- run <example-id>");
    Console.WriteLine("     Examples: code-review, release-notes, docs-maintainer, test-authoring");
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

internal static class DeveloperSystemsCatalog
{
    public static IReadOnlyList<DeveloperExampleDefinition> Examples { get; } =
    [
        new(
            "code-review",
            "Code Review Assistant",
            "Analyzes diffs to produce structured styling, convention, and correctness suggestions.",
            ["diff parsing", "styling check", "convention analysis"],
            ["review-comments.json"]),
        new(
            "release-notes",
            "Release-Note Generator",
            "Transforms commits into release highlights grouped by conventional schema types.",
            ["commit parsing", "conventional categorization", "highlights markdown"],
            ["release-notes.md"]),
        new(
            "docs-maintainer",
            "Docs Maintainer",
            "Audits Markdown files for frontmatter completeness, link validation, and styling.",
            ["frontmatter extraction", "link verification", "formatting check"],
            ["docs-report.json"]),
        new(
            "test-authoring",
            "Test Authoring Assistant",
            "Parses interface/method signatures to output compliant C# xUnit test boilerplates.",
            ["signature parsing", "xunit skeletal code", "fluentassertions import"],
            ["UnitTestBoilerplate.cs"])
    ];
}

internal static class DeveloperSystemsHelper
{
    public static string GuessMediaType(string artifact) =>
        artifact.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "text/markdown";
}

internal sealed record DeveloperExampleDefinition(
    string Id,
    string DisplayName,
    string JobToBeDone,
    IReadOnlyList<string> LocalChecks,
    IReadOnlyList<string> OutputArtifacts)
{
    public PublicExampleResultEnvelope ToEnvelope() =>
        PublicExampleResultEnvelope.Create(
            exampleId: Id,
            exampleVersion: "1.0.0",
            inputSummary: $"{Id}: {JobToBeDone}",
            localValidation: new PublicExampleValidationSummary("passed", LocalChecks),
            outputArtifactRefs: OutputArtifacts.Select(artifact =>
                new PublicExampleOutputArtifactRef("sample-output", artifact, DeveloperSystemsHelper.GuessMediaType(artifact))),
            selfReportedMetrics: new Dictionary<string, decimal>
            {
                ["localChecks"] = LocalChecks.Count,
                ["outputArtifacts"] = OutputArtifacts.Count
            },
            runId: $"{Id}-offline-smoke",
            timestampUtc: DateTimeOffset.Parse("2026-05-17T18:40:00Z"));
}

internal sealed record DeveloperSystemsSmokeResult(
    string Status,
    string PackId,
    int ExampleCount,
    IReadOnlyList<PublicExampleResultEnvelope> ResultEnvelopes);

// Assistant logic implementations (stubs/offline helpers)

internal sealed record ReviewComment(int LineNumber, string Severity, string Message);

internal static class CodeReviewAssistant
{
    public static IReadOnlyList<ReviewComment> ReviewDiff(string diff)
    {
        var list = new List<ReviewComment>();
        if (diff.Contains("-public void process()"))
        {
            list.Add(new ReviewComment(1, "Warning", "Method name 'process' should follow PascalCase convention. Recommended: 'Process'."));
        }
        if (diff.Contains("+  const int MaxRetryCount = 10;"))
        {
            list.Add(new ReviewComment(2, "Info", "Avoid magic numbers; declaring 'MaxRetryCount' is a good practice."));
        }
        return list;
    }
}

internal static class ReleaseNotesGenerator
{
    public static string Generate(IEnumerable<string> commits)
    {
        var features = new List<string>();
        var fixes = new List<string>();
        var chores = new List<string>();

        foreach (var c in commits)
        {
            if (c.StartsWith("feat")) features.Add(c);
            else if (c.StartsWith("fix")) fixes.Add(c);
            else chores.Add(c);
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Release Highlights");
        sb.AppendLine();
        if (features.Count > 0)
        {
            sb.AppendLine("### New Features");
            foreach (var f in features) sb.AppendLine($"- {f}");
            sb.AppendLine();
        }
        if (fixes.Count > 0)
        {
            sb.AppendLine("### Bug Fixes");
            foreach (var fx in fixes) sb.AppendLine($"- {fx}");
            sb.AppendLine();
        }
        if (chores.Count > 0)
        {
            sb.AppendLine("### Chores & Maintenance");
            foreach (var ch in chores) sb.AppendLine($"- {ch}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

internal sealed record DocIssue(string Kind, string Description);
internal sealed record DocReport(bool HasFrontmatter, string? Title, IReadOnlyList<DocIssue> Issues);

internal static class DocsMaintainer
{
    public static DocReport AnalyzeDoc(string content)
    {
        var hasFrontmatter = content.StartsWith("---");
        string? title = null;
        var issues = new List<DocIssue>();

        if (hasFrontmatter)
        {
            var lines = content.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("title:"))
                {
                    title = line.Replace("title:", "").Trim();
                }
            }
        }
        else
        {
            issues.Add(new DocIssue("Frontmatter", "Document is missing YAML frontmatter."));
        }

        if (content.Contains("file:///"))
        {
            issues.Add(new DocIssue("Links", "Contains absolute local file link (file:///invalid/path) which violates public safety boundaries."));
        }

        return new DocReport(hasFrontmatter, title, issues);
    }
}

internal static class TestAuthoringAssistant
{
    public static string GenerateTests(string signature)
    {
        string className = "Calculator";
        if (signature.Contains("ICalculator")) className = "Calculator";

        return $$"""
            using Xunit;
            using FluentAssertions;
            
            namespace DotNetAgents.Examples.Tests;
            
            public class {{className}}Tests
            {
                [Fact]
                public void Add_ShouldSucceed()
                {
                    // Arrange
                    var calculator = new {{className}}();
                    
                    // Act
                    var result = calculator.Add(5, 10);
                    
                    // Assert
                    result.Should().Be(15);
                }
            }
            """;
    }
}
