using System.Globalization;

namespace SalesArena.Manager.Web.Services.BossOffice;

/// <summary>
/// Loads a flat key:value cost catalog YAML file (no external YAML dependency).
/// </summary>
public sealed class BossOfficeCostCatalogLoader
{
    private readonly IWebHostEnvironment _environment;

    public BossOfficeCostCatalogLoader(IWebHostEnvironment environment) => _environment = environment;

    public BossOfficeCostCatalog Load()
    {
        var path = Path.Combine(_environment.WebRootPath, "config", "boss-office-cost-catalog.yaml");
        if (!File.Exists(path))
        {
            return CreateDefaults();
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            values[key] = value;
        }

        var asOf = ParseDate(values, "asOfUtc") ?? DateTimeOffset.UtcNow;
        var costPerTouch = ParseDecimal(values, "costPerTouchUsd", 0.42m);

        var tiers = new List<ModelTierSpend>
        {
            new("frontier", ParseDecimal(values, "tier_frontier_spend_usd", 1840m)),
            new("standard", ParseDecimal(values, "tier_standard_spend_usd", 620m)),
            new("economy", ParseDecimal(values, "tier_economy_spend_usd", 210m)),
        };

        return new BossOfficeCostCatalog(asOf, costPerTouch, tiers);
    }

    private static BossOfficeCostCatalog CreateDefaults() =>
        new(
            DateTimeOffset.UtcNow,
            0.42m,
            [
                new ModelTierSpend("frontier", 1840m),
                new ModelTierSpend("standard", 620m),
                new ModelTierSpend("economy", 210m),
            ]);

    private static decimal ParseDecimal(IReadOnlyDictionary<string, string> values, string key, decimal fallback) =>
        values.TryGetValue(key, out var raw) && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static DateTimeOffset? ParseDate(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var raw) && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
}
