using System.Text.Json;

namespace SalesArena.Manager.Web.Services.MoneyMap;

/// <summary>
/// Projects CC0 GeoJSON polygons into SVG path strings for the Money Map widget.
/// </summary>
public sealed class MoneyMapGeoJsonPaths
{
    private const double UsLonMin = -125.0;
    private const double UsLonMax = -66.5;
    private const double UsLatMin = 24.0;
    private const double UsLatMax = 49.5;
    private const double UsWidth = 960;
    private const double UsHeight = 520;

    private const double WorldLonMin = -130.0;
    private const double WorldLonMax = 155.0;
    private const double WorldLatMin = -55.0;
    private const double WorldLatMax = 55.0;
    private const double WorldWidth = 960;
    private const double WorldHeight = 480;

    public MoneyMapGeoJsonPaths(IWebHostEnvironment environment)
    {
        UsPath = LoadPath(environment, "us-simplified-cc0.geojson", ProjectUs);
        WorldPath = LoadPath(environment, "world-simplified-cc0.geojson", ProjectWorld);
    }

    public string UsPath { get; }

    public string WorldPath { get; }

    private static string LoadPath(
        IWebHostEnvironment environment,
        string fileName,
        Func<double, double, (double X, double Y)> projector)
    {
        var path = Path.Combine(environment.WebRootPath, "data", fileName);
        if (!File.Exists(path))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("features", out var features))
            {
                return string.Empty;
            }

            var segments = new List<string>();
            foreach (var feature in features.EnumerateArray())
            {
                if (!feature.TryGetProperty("geometry", out var geometry))
                {
                    continue;
                }

                var type = geometry.GetProperty("type").GetString();
                if (!geometry.TryGetProperty("coordinates", out var coordinates))
                {
                    continue;
                }

                if (string.Equals(type, "Polygon", StringComparison.Ordinal))
                {
                    segments.Add(PolygonToPath(coordinates[0], projector));
                }
            }

            return string.Join(" ", segments);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static string PolygonToPath(
        JsonElement ring,
        Func<double, double, (double X, double Y)> projector)
    {
        var parts = new List<string>();
        var first = true;
        foreach (var point in ring.EnumerateArray())
        {
            var lon = point[0].GetDouble();
            var lat = point[1].GetDouble();
            var (x, y) = projector(lon, lat);
            parts.Add(first ? $"M {x:F1} {y:F1}" : $"L {x:F1} {y:F1}");
            first = false;
        }

        parts.Add("Z");
        return string.Join(" ", parts);
    }

    private static (double X, double Y) ProjectUs(double lon, double lat)
    {
        var x = (lon - UsLonMin) / (UsLonMax - UsLonMin) * UsWidth;
        var y = (UsLatMax - lat) / (UsLatMax - UsLatMin) * UsHeight;
        return (x, y);
    }

    private static (double X, double Y) ProjectWorld(double lon, double lat)
    {
        var x = (lon - WorldLonMin) / (WorldLonMax - WorldLonMin) * WorldWidth;
        var y = (WorldLatMax - lat) / (WorldLatMax - WorldLatMin) * WorldHeight;
        return (x, y);
    }
}
