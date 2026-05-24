using System.Text;
using FluentAssertions;
using SalesArena.PersonaPackFormat;
using Xunit;

namespace SalesArena.Personas.Tests;

/// <summary>
/// SA-07-06 acceptance: 3 seeded packs (influencer / engineer / hardballer)
/// load through the SA-07-01 pack codec, ship a ≥ 300-word bio, ship ≥ 5
/// outreach touches, and are pairwise distinct.
/// </summary>
public sealed class CommunityPersonaPacksTests
{
    private const double DistinctnessThreshold = 0.30; // cosine-distance floor

    public static IEnumerable<object[]> PersonaSlugs() => new[]
    {
        new object[] { "influencer" },
        new object[] { "engineer" },
        new object[] { "hardballer" },
    };

    [Theory]
    [MemberData(nameof(PersonaSlugs))]
    public void Pack_directory_exists_and_has_required_files(string slug)
    {
        var dir = FindPersonaDir(slug);
        Directory.Exists(dir).Should().BeTrue($"{slug} pack dir must exist");

        foreach (var required in new[] { "persona.yaml", "system-prompt.md", "bio.md", "cadence.yaml", "avatar.svg", "proposals/starter.md", "README.md" })
        {
            File.Exists(Path.Combine(dir, required)).Should().BeTrue($"{slug}/{required} must exist");
        }

        // 5 outreach email variants per acceptance ("5 case-study touches it's optimized for").
        for (var i = 1; i <= 5; i++)
        {
            File.Exists(Path.Combine(dir, "outreach", "email", $"variant-{i}.md"))
                .Should().BeTrue($"{slug}/outreach/email/variant-{i}.md must exist");
        }
    }

    [Theory]
    [MemberData(nameof(PersonaSlugs))]
    public void Bio_is_at_least_300_words(string slug)
    {
        var bio = File.ReadAllText(Path.Combine(FindPersonaDir(slug), "bio.md"));
        var words = bio.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        words.Length.Should().BeGreaterOrEqualTo(300, $"{slug} bio must be at least 300 words per SA-07-06 acceptance");
    }

    [Theory]
    [MemberData(nameof(PersonaSlugs))]
    public async Task Pack_round_trips_through_SA_07_01_zip_codec(string slug)
    {
        var dir = FindPersonaDir(slug);
        var pack = BuildPackFromDirectory(slug, dir);

        var format = new ZipPersonaPackFormat();
        await using var ms = new MemoryStream();
        await format.ExportAsync(pack, ms);
        ms.Position = 0;
        var imported = await format.ImportAsync(ms);

        imported.Name.Should().Be(pack.Name);
        imported.Author.Should().Be(pack.Author);
        imported.Files.Keys.Should().BeEquivalentTo(pack.Files.Keys);
        foreach (var (path, bytes) in pack.Files)
        {
            imported.Files[path].Should().Equal(bytes, $"{slug}/{path} must round-trip byte-for-byte");
        }
    }

    [Theory]
    [InlineData("influencer", "engineer")]
    [InlineData("influencer", "hardballer")]
    [InlineData("engineer", "hardballer")]
    public void Pairwise_system_prompts_are_distinct(string a, string b)
    {
        var promptA = File.ReadAllText(Path.Combine(FindPersonaDir(a), "system-prompt.md"));
        var promptB = File.ReadAllText(Path.Combine(FindPersonaDir(b), "system-prompt.md"));

        var distance = 1.0 - BagOfWordsCosine(promptA, promptB);
        distance.Should().BeGreaterThan(DistinctnessThreshold,
            $"'{a}' vs '{b}' system-prompts must be at least {DistinctnessThreshold} apart in cosine distance (got {distance:F3})");
    }

    [Fact]
    public void Three_personas_have_distinct_tag_sets()
    {
        var influencer = ReadYamlTags("influencer");
        var engineer = ReadYamlTags("engineer");
        var hardballer = ReadYamlTags("hardballer");

        influencer.Intersect(engineer).Should().BeEmpty("influencer + engineer should not share any seed tag");
        influencer.Intersect(hardballer).Should().BeEmpty("influencer + hardballer should not share any seed tag");
        engineer.Intersect(hardballer).Should().BeEmpty("engineer + hardballer should not share any seed tag");
    }

    private static IReadOnlySet<string> ReadYamlTags(string slug)
    {
        var path = Path.Combine(FindPersonaDir(slug), "persona.yaml");
        var lines = File.ReadAllLines(path);
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inTags = false;
        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
            {
                inTags = true;
                continue;
            }
            if (inTags)
            {
                if (line.Length == 0 || (!line.StartsWith(' ') && !line.StartsWith('-') && !line.StartsWith('\t')))
                {
                    break;
                }
                var trimmed = line.TrimStart().TrimStart('-').Trim();
                if (trimmed.Length > 0)
                {
                    tags.Add(trimmed);
                }
            }
        }
        return tags;
    }

    private static PersonaPack BuildPackFromDirectory(string slug, string dir)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var fullPath in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(dir, fullPath).Replace('\\', '/');
            files[relative] = File.ReadAllBytes(fullPath);
        }
        return new PersonaPack(
            Name: slug,
            Author: "dna-community-seed",
            Version: "0.1.0",
            Files: files);
    }

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
        if (normA == 0 || normB == 0)
        {
            return 0.0;
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
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
            }
            else
            {
                if (token.Length > 0)
                {
                    AddIfMeaningful(bag, token.ToString());
                    token.Clear();
                }
            }
        }
        if (token.Length > 0)
        {
            AddIfMeaningful(bag, token.ToString());
        }
        return bag;
    }

    private static readonly HashSet<string> _stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "if", "in", "on", "at", "to", "of",
        "for", "with", "by", "as", "is", "it", "be", "are", "was", "were", "this",
        "that", "you", "your", "i", "we", "they", "them", "their", "his", "her",
        "do", "does", "did", "not", "no", "yes", "so", "out", "from", "up", "down",
        "have", "has", "had", "will", "would", "should", "can", "could", "may",
        "might", "shall", "than", "then", "into", "about", "over", "between",
        "any", "all", "more", "less", "few", "much",
    };

    private static void AddIfMeaningful(Dictionary<string, int> bag, string token)
    {
        if (token.Length < 3 || _stopwords.Contains(token)) return;
        bag[token] = bag.TryGetValue(token, out var existing) ? existing + 1 : 1;
    }

    private static string FindPersonaDir(string slug)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "personas", "community", slug),
            Path.Combine(AppContext.BaseDirectory, "personas", "community", slug),
        };
        foreach (var c in candidates)
        {
            var resolved = Path.GetFullPath(c);
            if (Directory.Exists(resolved))
            {
                return resolved;
            }
        }
        throw new DirectoryNotFoundException(
            $"could not locate community persona '{slug}'; checked: {string.Join(", ", candidates)}");
    }
}
