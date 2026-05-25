namespace SalesArena.Manager.Web.Services.Gallery;

public sealed class CommunityPersonaGalleryCatalog : ICommunityPersonaGalleryCatalog
{
    private readonly string _communityRoot;
    private readonly object _challengeLock = new();
    private readonly List<string> _challengeQueue = [];

    public CommunityPersonaGalleryCatalog(IWebHostEnvironment environment)
    {
        _communityRoot = ResolveCommunityRoot(environment.ContentRootPath);
    }

    public string CommunityRootPath => _communityRoot;

    public async Task<CommunityPersonaGalleryIndex> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cards = await Task.Run(() => ScanCards(), cancellationToken).ConfigureAwait(false);
        var tags = cards.SelectMany(c => c.Tags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new CommunityPersonaGalleryIndex(cards, tags);
    }

    public async Task<CommunityPersonaDetail?> GetDetailAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Task.Run(() => BuildDetail(slug), cancellationToken).ConfigureAwait(false);
    }

    public string? QueueChallenge(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        lock (_challengeLock)
        {
            _challengeQueue.Add(slug.Trim());
        }

        return slug.Trim();
    }

    public IReadOnlyList<string> PeekChallengeQueue()
    {
        lock (_challengeLock)
        {
            return _challengeQueue.ToList();
        }
    }

    private IReadOnlyList<CommunityPersonaCard> ScanCards()
    {
        if (!Directory.Exists(_communityRoot))
        {
            return [];
        }

        var cards = new List<CommunityPersonaCard>();
        foreach (var dir in Directory.EnumerateDirectories(_communityRoot))
        {
            var slug = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(slug) || slug.StartsWith('.'))
            {
                continue;
            }

            var card = TryBuildCard(slug, dir);
            if (card is not null)
            {
                cards.Add(card);
            }
        }

        return cards.OrderByDescending(c => c.Elo).ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private CommunityPersonaDetail? BuildDetail(string slug)
    {
        var dir = Path.Combine(_communityRoot, slug);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        var card = TryBuildCard(slug, dir);
        if (card is null)
        {
            return null;
        }

        var bioPath = Path.Combine(dir, "bio.md");
        var promptPath = Path.Combine(dir, "system-prompt.md");
        var bio = File.Exists(bioPath) ? File.ReadAllText(bioPath) : string.Empty;
        var prompt = File.Exists(promptPath) ? File.ReadAllText(promptPath) : string.Empty;

        return new CommunityPersonaDetail(
            card,
            bio,
            prompt,
            BuildRecentContests(slug));
    }

    private CommunityPersonaCard? TryBuildCard(string slug, string dir)
    {
        var yamlPath = Path.Combine(dir, "persona.yaml");
        if (!File.Exists(yamlPath))
        {
            return null;
        }

        var meta = PersonaYamlParser.Parse(File.ReadAllText(yamlPath));
        var bioPath = Path.Combine(dir, "bio.md");
        var bio = File.Exists(bioPath) ? File.ReadAllText(bioPath) : string.Empty;
        var stats = DemoStats.ForSlug(slug);
        var author = string.IsNullOrWhiteSpace(meta.Author) ? "anonymous" : meta.Author.Trim();
        var displayName = string.IsNullOrWhiteSpace(meta.Name) ? slug : meta.Name.Trim();
        var isSigned = !string.Equals(author, "anonymous", StringComparison.OrdinalIgnoreCase);

        return new CommunityPersonaCard(
            slug,
            displayName,
            author,
            GalleryTextSanitizer.ToPlainExcerpt(bio),
            meta.Tags,
            stats.ContestsRun,
            stats.Wins,
            stats.DealsClosed,
            stats.AvgConversionPercent,
            stats.Elo,
            isSigned,
            $"/community-personas/{slug}/avatar.svg");
    }

    private static IReadOnlyList<CommunityPersonaContestSummary> BuildRecentContests(string slug)
    {
        var seed = DemoStats.HashSlug(slug);
        var now = DateTimeOffset.UtcNow;
        return
        [
            new($"Tuesday Steak-Knives #{seed % 97}", seed % 3 == 0 ? "Won" : "Top 3", now.AddDays(-(seed % 14 + 3))),
            new("Glengarry sprint", seed % 2 == 0 ? "Runner-up" : "Mid-pack", now.AddDays(-(seed % 30 + 15))),
            new("Inbound blitz demo", "Participated", now.AddDays(-(seed % 45 + 30))),
        ];
    }

    public static string ResolveCommunityRoot(string contentRootPath)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "personas", "community")),
            Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "..", "personas", "community")),
            Path.GetFullPath(Path.Combine(contentRootPath, "personas", "community")),
        };

        foreach (var candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates[0];
    }

    private static class DemoStats
    {
        public static (int ContestsRun, int Wins, int DealsClosed, double AvgConversionPercent, int Elo) ForSlug(string slug)
        {
            var h = HashSlug(slug);
            var contests = 8 + (h % 40);
            var wins = Math.Max(1, contests / (3 + (h % 4)));
            var deals = wins * (2 + (h % 5));
            var conversion = 8.0 + (h % 25) + (h % 10) * 0.1;
            var elo = 1050 + (h % 450);
            return (contests, wins, deals, conversion, elo);
        }

        public static int HashSlug(string slug)
        {
            var hash = 17;
            foreach (var ch in slug)
            {
                hash = (hash * 31) + ch;
            }

            return Math.Abs(hash);
        }
    }

    private static class PersonaYamlParser
    {
        public static (string Name, string Author, IReadOnlyList<string> Tags) Parse(string yaml)
        {
            var name = string.Empty;
            var author = string.Empty;
            var tags = new List<string>();
            var inTags = false;

            foreach (var raw in yaml.Split('\n'))
            {
                var line = raw.TrimEnd();
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("name:", StringComparison.Ordinal))
                {
                    name = Unquote(trimmed["name:".Length..].Trim());
                    inTags = false;
                }
                else if (trimmed.StartsWith("author:", StringComparison.Ordinal))
                {
                    author = Unquote(trimmed["author:".Length..].Trim());
                    inTags = false;
                }
                else if (trimmed.StartsWith("tags:", StringComparison.Ordinal))
                {
                    inTags = true;
                }
                else if (inTags && trimmed.StartsWith("- ", StringComparison.Ordinal))
                {
                    tags.Add(Unquote(trimmed[2..].Trim()));
                }
                else if (inTags && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith('-'))
                {
                    inTags = false;
                }
            }

            return (name, author, tags);
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                return value[1..^1];
            }

            return value;
        }
    }
}
