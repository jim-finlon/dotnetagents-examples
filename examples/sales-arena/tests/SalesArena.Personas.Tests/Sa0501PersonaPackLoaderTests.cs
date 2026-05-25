using System.Text;
using FluentAssertions;
using SalesArena.PersonaPackFormat;
using Xunit;

namespace SalesArena.Personas.Tests;

/// <summary>SA-05-01 regression coverage for the six built-in Sales Arena persona packs.</summary>
public sealed class Sa0501PersonaPackLoaderTests
{
    private const double DistinctnessThreshold = 0.35;
    private static readonly string[] BasePersonaSlugs =
    [
        "roma",
        "levene",
        "moss",
        "aaronow",
        "williamson",
        "mitch-and-murray",
    ];

    public static IEnumerable<object[]> BasePersonaData() =>
        BasePersonaSlugs.Select(slug => new object[] { slug });

    [Theory]
    [MemberData(nameof(BasePersonaData))]
    public async Task Loader_validates_required_files_yaml_cadence_and_performance_budget(string slug)
    {
        var result = await new PersonaPackLoader().LoadAsync(FindPersonaDir(slug));

        result.Name.Should().NotBeNullOrWhiteSpace();
        result.Author.Should().Be("dna-sales-arena");
        result.Version.Should().Be("1.0.0");
        result.ModelTier.Should().NotBeNullOrWhiteSpace();
        result.OutreachVariantPaths.Should().Contain(path => path.StartsWith("outreach/", StringComparison.OrdinalIgnoreCase));
        result.LoadElapsed.Should().BeLessThan(
            TimeSpan.FromMilliseconds(100),
            "PersonaPackLoader.LoadAsync should stay under the SA-05-01 per-pack CI budget");

        if (slug == "mitch-and-murray")
        {
            result.NarratorOnly.Should().BeTrue("Mitch & Murray is the narrator-only pack");
            result.ChannelMixTotal.Should().BeApproximately(0.0, 0.001);
        }
        else
        {
            result.NarratorOnly.Should().BeFalse();
            result.ChannelMixTotal.Should().BeApproximately(100.0, 0.001);
            CountWords(result.SystemPrompt).Should().BeGreaterOrEqualTo(200);
        }
    }

    [Fact]
    public async Task Deterministic_llm_stub_outputs_are_pairwise_distinct_for_base_personas()
    {
        var loader = new PersonaPackLoader();
        var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var slug in BasePersonaSlugs)
        {
            var pack = await loader.LoadAsync(FindPersonaDir(slug));
            outputs[slug] = DeterministicPersonaLlmStub.Respond(
                pack,
                "A skeptical VP asks whether the revenue platform can prove payback before the renewal date.");
        }

        foreach (var a in BasePersonaSlugs)
        {
            foreach (var b in BasePersonaSlugs.Where(slug => string.CompareOrdinal(slug, a) > 0))
            {
                var distance = 1.0 - BagOfWordsCosine(outputs[a], outputs[b]);
                distance.Should().BeGreaterThan(
                    DistinctnessThreshold,
                    $"{a} and {b} deterministic persona outputs must stay distinct enough to matter (got {distance:F3})");
            }
        }
    }

    private static string FindPersonaDir(string slug)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "personas", slug),
            Path.Combine(AppContext.BaseDirectory, "personas", slug),
        };
        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
                return resolved;
        }

        throw new DirectoryNotFoundException(
            $"could not locate base persona '{slug}'; checked: {string.Join(", ", candidates)}");
    }

    private static int CountWords(string text) =>
        text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static double BagOfWordsCosine(string a, string b)
    {
        var bagA = Tokenize(a);
        var bagB = Tokenize(b);

        var allKeys = new HashSet<string>(bagA.Keys, StringComparer.OrdinalIgnoreCase);
        allKeys.UnionWith(bagB.Keys);

        double dot = 0, normA = 0, normB = 0;
        foreach (var key in allKeys)
        {
            bagA.TryGetValue(key, out var av);
            bagB.TryGetValue(key, out var bv);
            dot += av * bv;
            normA += av * av;
            normB += bv * bv;
        }

        return normA == 0 || normB == 0
            ? 0.0
            : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    private static Dictionary<string, int> Tokenize(string text)
    {
        var bag = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var token = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetter(c))
            {
                token.Append(char.ToLowerInvariant(c));
                continue;
            }

            AddIfMeaningful(bag, token);
        }

        AddIfMeaningful(bag, token);
        return bag;
    }

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "if", "in", "on", "at", "to", "of",
        "for", "with", "by", "as", "is", "it", "be", "are", "was", "were", "this",
        "that", "you", "your", "i", "we", "they", "them", "their", "his", "her",
        "do", "does", "did", "not", "no", "yes", "so", "out", "from", "up", "down",
        "have", "has", "had", "will", "would", "should", "can", "could", "may",
        "might", "shall", "than", "then", "into", "about", "over", "between",
        "any", "all", "more", "less", "few", "much",
    };

    private static void AddIfMeaningful(Dictionary<string, int> bag, StringBuilder token)
    {
        if (token.Length == 0)
            return;

        var value = token.ToString();
        token.Clear();
        if (value.Length < 3 || Stopwords.Contains(value))
            return;

        bag[value] = bag.TryGetValue(value, out var existing) ? existing + 1 : 1;
    }

    private static class DeterministicPersonaLlmStub
    {
        public static string Respond(PersonaPackLoadResult pack, string input)
        {
            var signature = string.Join(
                ' ',
                Tokenize(pack.SystemPrompt)
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(80)
                    .Select(pair => pair.Key));

            return $"{pack.Name}. narrator={pack.NarratorOnly}. input={input}. signature={signature}";
        }
    }
}
