namespace SalesArena.Manager.Web.Services.MoneyMap;

/// <summary>
/// Resolves persona slug → display primary color from community <c>persona.yaml</c> when present.
/// </summary>
public sealed class PersonaDisplayColorCatalog
{
    private static readonly IReadOnlyDictionary<string, string> DemoPersonaColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["roma"] = "#C41E3A",
            ["romano"] = "#C41E3A",
            ["levene"] = "#1B4D89",
            ["moss"] = "#2D6A4F",
            ["aaronow"] = "#6C757D",
            ["williamson"] = "#E85D04",
            ["harris"] = "#7B2CBF",
        };

    private readonly string _communityRoot;

    public PersonaDisplayColorCatalog(IWebHostEnvironment environment)
    {
        _communityRoot = Gallery.CommunityPersonaGalleryCatalog.ResolveCommunityRoot(environment.ContentRootPath);
    }

    public string GetPrimaryColor(string personaSlug)
    {
        if (string.IsNullOrWhiteSpace(personaSlug))
        {
            return "#5A6FFF";
        }

        var slug = personaSlug.Trim().ToLowerInvariant();
        if (DemoPersonaColors.TryGetValue(slug, out var demo))
        {
            return demo;
        }

        var yamlPath = Path.Combine(_communityRoot, slug, "persona.yaml");
        if (!File.Exists(yamlPath))
        {
            return FallbackFromHash(slug);
        }

        var color = TryParsePrimaryColor(File.ReadAllText(yamlPath));
        return color ?? FallbackFromHash(slug);
    }

    private static string? TryParsePrimaryColor(string yaml)
    {
        var inDisplay = false;
        foreach (var raw in yaml.Split('\n'))
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith("display:", StringComparison.Ordinal))
            {
                inDisplay = true;
                continue;
            }

            if (inDisplay && trimmed.StartsWith("primary_color:", StringComparison.Ordinal))
            {
                return Unquote(trimmed["primary_color:".Length..].Trim());
            }

            if (inDisplay && !trimmed.StartsWith(' ') && !trimmed.StartsWith('\t') && trimmed.Length > 0 && !trimmed.StartsWith('#'))
            {
                inDisplay = false;
            }
        }

        return null;
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

    private static string FallbackFromHash(string slug)
    {
        var hue = Math.Abs(StringComparer.Ordinal.GetHashCode(slug)) % 360;
        return $"hsl({hue}, 65%, 45%)";
    }
}
