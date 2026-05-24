using System.Globalization;
using System.Text.RegularExpressions;

namespace SalesArena.Manager.Web.Services.MoneyMap;

/// <summary>
/// Loads forkable region → SVG coordinate mapping from <c>wwwroot/config/region-coordinates.v1.yaml</c>.
/// </summary>
public sealed class RegionCoordinateCatalog
{
    private readonly IReadOnlyDictionary<string, RegionPoint> _points;

    public RegionCoordinateCatalog(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.WebRootPath, "config", "region-coordinates.v1.yaml");
        var merged = new Dictionary<string, RegionPoint>(BuildDefaults(), StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path))
        {
            foreach (var (key, point) in ParseYaml(File.ReadAllText(path)))
            {
                merged[key] = point;
            }
        }

        _points = merged;
    }

    public bool TryGetPoint(string regionCode, out RegionPoint point) =>
        _points.TryGetValue(regionCode.Trim(), out point!);

    public string ResolveRegionForLead(string leadId)
    {
        var usCodes = _points.Keys.Where(k => k.StartsWith("us-", StringComparison.Ordinal)).OrderBy(k => k).ToList();
        if (usCodes.Count > 0)
        {
            var hash = Math.Abs(StringComparer.Ordinal.GetHashCode(leadId));
            return usCodes[hash % usCodes.Count];
        }

        var worldCodes = _points.Keys.Where(k => !k.StartsWith("us-", StringComparison.Ordinal)).OrderBy(k => k).ToList();
        if (worldCodes.Count > 0)
        {
            var hash = Math.Abs(StringComparer.Ordinal.GetHashCode(leadId));
            return worldCodes[hash % worldCodes.Count];
        }

        return "us-midwest";
    }

    private static IReadOnlyDictionary<string, RegionPoint> ParseYaml(string yaml)
    {
        var result = new Dictionary<string, RegionPoint>(StringComparer.OrdinalIgnoreCase);
        string? currentSection = null;
        string? currentKey = null;
        double? x = null;
        double? y = null;
        string? label = null;

        void Flush()
        {
            if (currentKey is not null && x is not null && y is not null)
            {
                result[currentKey] = new RegionPoint(x.Value, y.Value, label ?? currentKey);
            }

            currentKey = null;
            x = null;
            y = null;
            label = null;
        }

        foreach (var raw in yaml.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            if (line is "us:" or "world:")
            {
                Flush();
                currentSection = line[..^1];
                continue;
            }

            var keyMatch = Regex.Match(line, @"^([a-z0-9-]+):\s*\{");
            if (keyMatch.Success)
            {
                Flush();
                currentKey = keyMatch.Groups[1].Value;
                var xMatch = Regex.Match(line, @"\bx:\s*([0-9.]+)");
                var yMatch = Regex.Match(line, @"\by:\s*([0-9.]+)");
                var labelMatch = Regex.Match(line, @"label:\s*""([^""]+)""");
                if (xMatch.Success)
                {
                    x = double.Parse(xMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                }

                if (yMatch.Success)
                {
                    y = double.Parse(yMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                }

                if (labelMatch.Success)
                {
                    label = labelMatch.Groups[1].Value;
                }
            }
        }

        Flush();
        return result.Count > 0 ? result : BuildDefaults();
    }

    private static IReadOnlyDictionary<string, RegionPoint> BuildDefaults() =>
        new Dictionary<string, RegionPoint>(StringComparer.OrdinalIgnoreCase)
        {
            ["us-northeast"] = new(820, 165, "US Northeast"),
            ["us-midwest"] = new(580, 200, "US Midwest"),
            ["emea-uk"] = new(480, 140, "EMEA UK"),
        };

    public sealed record RegionPoint(double X, double Y, string Label);
}
