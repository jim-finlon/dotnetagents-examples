using System.Text;
using FluentAssertions;
using SalesArena.PersonaPackFormat;
using Xunit;

namespace SalesArena.Personas.Tests;

/// <summary>
/// SA-06-05 fork-your-persona tutorial drift fixture. Loads the
/// <c>the-silent-one</c> community pack — the worked example from
/// <c>docs/public/SALES-ARENA-FORK-YOUR-PERSONA.md</c> — and asserts:
/// (a) the pack ships every required file, (b) the bio meets the
/// SA-07-06 300-word floor, (c) the pack round-trips through the
/// SA-07-01 zip codec, (d) the system prompt is distinct from each of
/// the three other seeded community packs (Roma-sparring partner
/// proxy: cosine distance ≥ 0.30 versus influencer / engineer /
/// hardballer), and (e) the cadence enforces the persona's no-voicemails
/// channel-mix rule.
/// </summary>
public sealed class TheSilentOnePackTests
{
    private const string Slug = "the-silent-one";
    private const double DistinctnessThreshold = 0.30;
    private const int SystemPromptMinWords = 200;

    private static readonly string[] OtherCommunitySlugs = { "influencer", "engineer", "hardballer" };

    [Fact]
    public void Pack_directory_exists_and_ships_every_required_file()
    {
        var dir = FindPersonaDir(Slug);
        Directory.Exists(dir).Should().BeTrue($"{Slug} pack dir must exist");

        foreach (var required in new[]
        {
            "persona.yaml",
            "system-prompt.md",
            "bio.md",
            "cadence.yaml",
            "avatar.svg",
            "proposals/starter.md",
            "README.md",
        })
        {
            File.Exists(Path.Combine(dir, required)).Should().BeTrue($"{Slug}/{required} must exist");
        }

        for (var i = 1; i <= 5; i++)
        {
            File.Exists(Path.Combine(dir, "outreach", "email", $"variant-{i}.md"))
                .Should().BeTrue($"{Slug}/outreach/email/variant-{i}.md must exist");
        }
    }

    [Fact]
    public void Bio_is_at_least_300_words()
    {
        var bio = File.ReadAllText(Path.Combine(FindPersonaDir(Slug), "bio.md"));
        var words = bio.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        words.Length.Should().BeGreaterOrEqualTo(300, "SA-07-06 community-pack acceptance bio floor");
    }

    [Fact]
    public void System_prompt_meets_persona_pack_200_word_floor()
    {
        var prompt = File.ReadAllText(Path.Combine(FindPersonaDir(Slug), "system-prompt.md"));
        var words = prompt.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        words.Length.Should().BeGreaterOrEqualTo(
            SystemPromptMinWords,
            "fork-your-persona tutorial calls out a 200-word floor (under it, the validator flags you)");
    }

    [Fact]
    public async Task Pack_round_trips_through_SA_07_01_zip_codec()
    {
        var dir = FindPersonaDir(Slug);
        var pack = BuildPackFromDirectory(Slug, dir);

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
            imported.Files[path].Should().Equal(bytes, $"{Slug}/{path} must round-trip byte-for-byte");
        }
    }

    [Theory]
    [InlineData("influencer")]
    [InlineData("engineer")]
    [InlineData("hardballer")]
    public void System_prompt_is_distinct_from_each_other_community_pack(string other)
    {
        var promptA = File.ReadAllText(Path.Combine(FindPersonaDir(Slug), "system-prompt.md"));
        var promptB = File.ReadAllText(Path.Combine(FindPersonaDir(other), "system-prompt.md"));

        var distance = 1.0 - BagOfWordsCosine(promptA, promptB);
        distance.Should().BeGreaterThan(
            DistinctnessThreshold,
            $"'{Slug}' vs '{other}' system-prompts must be at least {DistinctnessThreshold} apart in cosine distance (got {distance:F3})");
    }

    [Fact]
    public void Tag_set_is_disjoint_from_each_other_community_pack()
    {
        var ours = ReadYamlTags(Slug);
        foreach (var other in OtherCommunitySlugs)
        {
            var theirs = ReadYamlTags(other);
            ours.Intersect(theirs)
                .Should()
                .BeEmpty($"{Slug} + {other} must not share any seed tag (worked-example distinctness)");
        }
    }

    [Fact]
    public void Cadence_channel_mix_sums_to_one_and_voicemails_are_disabled()
    {
        var cadenceText = File.ReadAllText(Path.Combine(FindPersonaDir(Slug), "cadence.yaml"));

        var channelMixSum = ParseChannelMixSum(cadenceText);
        channelMixSum.Should().BeApproximately(
            1.0,
            0.001,
            "tutorial says channel mix must sum to 1.0 or the matchmaker normalizes weirdly");

        cadenceText.Should().Contain(
            "will_leave_voicemail: false",
            "The Silent One refuses voicemails — that is the persona's no-go list, not a default");
    }

    [Fact]
    public void Persona_yaml_pins_model_tier_to_local_strong()
    {
        var yaml = File.ReadAllText(Path.Combine(FindPersonaDir(Slug), "persona.yaml"));
        yaml.Should().Contain(
            "model_tier: local-strong",
            "tutorial worked-example explicitly pins model_tier=local-strong");
    }

    [Fact]
    public void Outreach_variant_1_matches_the_tutorial_worked_example_anchor()
    {
        var variant1 = File.ReadAllText(
            Path.Combine(FindPersonaDir(Slug), "outreach", "email", "variant-1.md"));

        variant1.Should().Contain("{{prospect.linkedin_headline}}",
            "tutorial variant 1 subject is a literal quote of the prospect's LinkedIn headline");
        variant1.Should().Contain("Want to tell me what you actually mean by it?",
            "tutorial variant 1 body asks the prospect to name their own meaning — drift guard");
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

    private static double ParseChannelMixSum(string cadenceText)
    {
        double sum = 0.0;
        var inChannelMix = false;
        foreach (var raw in cadenceText.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            if (line.StartsWith("channel_mix:", StringComparison.OrdinalIgnoreCase))
            {
                inChannelMix = true;
                continue;
            }
            if (!inChannelMix) continue;

            var trimmed = line.TrimStart();
            if (trimmed.Length == 0) continue;
            if (!line.StartsWith("  ") && !line.StartsWith("\t"))
            {
                // exited channel_mix block
                break;
            }

            var colon = trimmed.IndexOf(':');
            if (colon < 0) continue;
            var rhs = trimmed[(colon + 1)..].Trim();
            if (double.TryParse(rhs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var weight))
            {
                sum += weight;
            }
        }
        return sum;
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
