using System.Diagnostics;
using DotNetAgents.Knowledge;
using DotNetAgents.Knowledge.Models;
using DotNetAgents.Knowledge.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace SalesArena.Knowledge.Tests;

public sealed class SalesArenaKnowledgeCorpusTests
{
    private static readonly string KnowledgeRoot = ResolveKnowledgeRoot();

    public static TheoryData<string, string, string, string> PersonaObjectionQueries => new()
    {
        { "Roma", "forecast was off by ten points less next quarter", "objections/01-price-too-high.md", "Roma" },
        { "Levene", "calibration window data walked back to their CFO", "objections/01-price-too-high.md", "Levene" },
        { "Moss", "committing eighty grand to the board", "objections/01-price-too-high.md", "Moss" },
        { "Aaronow", "loaded cost of one rep for one week", "objections/01-price-too-high.md", "Aaronow" },
        { "Williamson", "fifty-five dollars a month", "objections/01-price-too-high.md", "Williamson" },
        { "Mitch & Murray", "calibration intervals tightening from +/-20 to +/-10", "objections/01-price-too-high.md", "Mitch & Murray" },
    };

    [Fact]
    public void Knowledge_corpus_contains_required_markdown_volume()
    {
        var files = Directory.EnumerateFiles(KnowledgeRoot, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(KnowledgeRoot, path).Replace('\\', '/'))
            .ToList();

        files.Should().HaveCountGreaterThanOrEqualTo(40);
        files.Should().Contain("objections/01-price-too-high.md");
        files.Should().Contain("case-studies/01-greybridge-software.md");
        files.Should().Contain("product/02-analytics-engine.md");
    }

    [Fact]
    public async Task Knowledge_corpus_indexes_under_five_seconds()
    {
        var stopwatch = Stopwatch.StartNew();
        var index = await SalesArenaKnowledgeIndex.BuildAsync(KnowledgeRoot);
        stopwatch.Stop();

        index.FileCount.Should().BeGreaterThanOrEqualTo(40);
        index.SectionCount.Should().BeGreaterThan(index.FileCount);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [MemberData(nameof(PersonaObjectionQueries))]
    public async Task Persona_objection_queries_resolve_to_expected_sections(
        string persona,
        string query,
        string expectedFile,
        string expectedHeading)
    {
        var index = await SalesArenaKnowledgeIndex.BuildAsync(KnowledgeRoot);

        var result = await index.SearchTopAsync(query);

        result.Metadata["relativePath"].Should().Be(expectedFile);
        result.Metadata["heading"].Should().Be(expectedHeading);
        result.Description.Should().Contain(persona);
    }

    [Fact]
    public async Task Case_study_query_resolves_to_measured_outcome()
    {
        var index = await SalesArenaKnowledgeIndex.BuildAsync(KnowledgeRoot);

        var result = await index.SearchTopAsync("forecast-confidence interval narrowed from +/-21% to +/-9%");

        result.Metadata["relativePath"].Should().Be("case-studies/01-greybridge-software.md");
        result.Metadata["heading"].Should().Be("The measured outcome");
        result.Description.Should().Contain("forecast-confidence interval narrowed");
    }

    [Fact]
    public async Task Product_query_resolves_to_calibration_window_description()
    {
        var index = await SalesArenaKnowledgeIndex.BuildAsync(KnowledgeRoot);

        var result = await index.SearchTopAsync("first 90 days of data are the calibration window");

        result.Metadata["relativePath"].Should().Be("product/02-analytics-engine.md");
        result.Metadata["heading"].Should().Be("Calibration");
        result.Description.Should().Contain("first 90 days of data are the calibration window");
    }

    private static string ResolveKnowledgeRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "examples", "sales-arena", "knowledge");
            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate examples/sales-arena/knowledge from the test output directory.");
    }
}

internal sealed class SalesArenaKnowledgeIndex
{
    private readonly IKnowledgeRepository _repository;

    private SalesArenaKnowledgeIndex(IKnowledgeRepository repository, int fileCount, int sectionCount)
    {
        _repository = repository;
        FileCount = fileCount;
        SectionCount = sectionCount;
    }

    public int FileCount { get; }
    public int SectionCount { get; }

    public static async Task<SalesArenaKnowledgeIndex> BuildAsync(string knowledgeRoot, CancellationToken cancellationToken = default)
    {
        var repository = new KnowledgeRepository(new InMemoryKnowledgeStore(), NullLogger<KnowledgeRepository>.Instance);
        var files = Directory.EnumerateFiles(knowledgeRoot, "*.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var sectionCount = 0;
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(knowledgeRoot, file).Replace('\\', '/');
            var text = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            foreach (var section in MarkdownSection.Split(relativePath, text))
            {
                await repository.AddKnowledgeAsync(section.ToKnowledgeItem(), cancellationToken).ConfigureAwait(false);
                sectionCount++;
            }
        }

        return new SalesArenaKnowledgeIndex(repository, files.Count, sectionCount);
    }

    public async Task<KnowledgeItem> SearchTopAsync(string query, CancellationToken cancellationToken = default)
    {
        var results = await _repository.SearchKnowledgeAsync(query, includeGlobal: true, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        results.Should().NotBeEmpty($"query '{query}' should resolve to an indexed Sales Arena knowledge section");
        return results[0];
    }
}

internal sealed record MarkdownSection(
    string RelativePath,
    string Heading,
    string Content)
{
    public static IReadOnlyList<MarkdownSection> Split(string relativePath, string markdown)
    {
        var sections = new List<MarkdownSection>();
        var currentHeading = Path.GetFileNameWithoutExtension(relativePath);
        var current = new List<string>();

        foreach (var line in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (TryHeading(line, out var heading))
            {
                Flush();
                currentHeading = heading;
                current.Add(heading);
                continue;
            }

            current.Add(line);
        }

        Flush();
        return sections;

        void Flush()
        {
            var content = string.Join('\n', current).Trim();
            if (content.Length > 0)
            {
                sections.Add(new MarkdownSection(relativePath, currentHeading, content));
            }

            current.Clear();
        }
    }

    public KnowledgeItem ToKnowledgeItem()
    {
        var tags = RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new KnowledgeItem
        {
            Title = $"{RelativePath}#{Heading}",
            Description = NormalizeMarkdownText(Content),
            Context = $"Sales Arena knowledge corpus section '{Heading}' from {RelativePath}.",
            Category = KnowledgeCategory.BestPractice,
            Severity = KnowledgeSeverity.Info,
            Tags = tags,
            TechStack = new[] { "sales-arena", "knowledge-pack", "dotnetagents-knowledge" },
            Metadata = new Dictionary<string, string>
            {
                ["relativePath"] = RelativePath,
                ["heading"] = Heading
            }
        };
    }

    private static bool TryHeading(string line, out string heading)
    {
        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('#'))
        {
            heading = string.Empty;
            return false;
        }

        var hashCount = trimmed.TakeWhile(static c => c == '#').Count();
        if (hashCount is < 1 or > 6 || trimmed.Length <= hashCount || trimmed[hashCount] != ' ')
        {
            heading = string.Empty;
            return false;
        }

        heading = trimmed[(hashCount + 1)..].Trim();
        return heading.Length > 0;
    }

    private static string NormalizeMarkdownText(string markdown)
    {
        var lines = markdown
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(static line => line.Trim().TrimStart('>').Trim());

        return string.Join(' ', lines)
            .Replace("`", string.Empty, StringComparison.Ordinal)
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("±", "+/-", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal)
            .Trim();
    }
}
