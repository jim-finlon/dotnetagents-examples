using System.Text.Json;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Manager.Web.Services.MoneyMap;

/// <summary>
/// Projects <see cref="ArenaEventKinds.DealClosed"/> won deals into regional map pins.
/// </summary>
public sealed class MoneyMapState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan NewPinHighlight = TimeSpan.FromSeconds(3);

    private readonly RegionCoordinateCatalog _regions;
    private readonly PersonaDisplayColorCatalog _colors;
    private readonly MoneyMapGeoJsonPaths _paths;
    private readonly List<MoneyMapPin> _pins = [];
    private readonly Lock _gate = new();

    private long _lastAppliedEventId;
    private bool _wired;
    private ArenaLiveFeed? _feed;

    public MoneyMapState(
        RegionCoordinateCatalog regions,
        PersonaDisplayColorCatalog colors,
        MoneyMapGeoJsonPaths paths)
    {
        _regions = regions;
        _colors = colors;
        _paths = paths;
    }

    public event Action? StateChanged;

    public MoneyMapViewModel Snapshot
    {
        get
        {
            lock (_gate)
            {
                var now = DateTimeOffset.UtcNow;
                var pins = _pins
                    .Select(p => p with { IsNew = now - p.ClosedAtUtc < NewPinHighlight })
                    .ToList();

                var usPins = pins.Where(p => p.RegionCode.StartsWith("us-", StringComparison.Ordinal)).ToList();
                var worldPins = pins.Where(p => !p.RegionCode.StartsWith("us-", StringComparison.Ordinal)).ToList();
                var total = pins.Where(p => p.ValueUsd > 0).Sum(p => p.ValueUsd);

                return new MoneyMapViewModel(
                    usPins,
                    worldPins,
                    _paths.UsPath,
                    _paths.WorldPath,
                    total);
            }
        }
    }

    public void WireToFeed(ArenaLiveFeed feed)
    {
        if (_wired)
        {
            return;
        }

        _wired = true;
        _feed = feed;
        feed.EventsChanged += OnFeedEventsChanged;
        ApplyNewEvents(feed.RecentEvents);
    }

    public void UnwireFromFeed(ArenaLiveFeed feed)
    {
        if (!_wired)
        {
            return;
        }

        feed.EventsChanged -= OnFeedEventsChanged;
        _wired = false;
    }

    private void OnFeedEventsChanged()
    {
        if (_feed is not null)
        {
            ApplyNewEvents(_feed.RecentEvents);
        }
    }

    public void ApplyNewEvents(IReadOnlyList<ArenaEventMessage> events)
    {
        var ordered = events.Where(e => e.Id > _lastAppliedEventId).OrderBy(e => e.Id).ToList();
        if (ordered.Count == 0)
        {
            StateChanged?.Invoke();
            return;
        }

        lock (_gate)
        {
            foreach (var evt in ordered)
            {
                TryAddPin(evt);
                _lastAppliedEventId = Math.Max(_lastAppliedEventId, evt.Id);
            }
        }

        StateChanged?.Invoke();
    }

    private void TryAddPin(ArenaEventMessage evt)
    {
        if (!string.Equals(evt.Kind, ArenaEventKinds.DealClosed, StringComparison.Ordinal) ||
            evt.Persona is null ||
            evt.LeadId is null)
        {
            return;
        }

        var payload = Deserialize<DealClosedPayload>(evt.PayloadJson);
        if (payload is null || !string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_pins.Any(p => p.LeadId == evt.LeadId && p.Persona == evt.Persona))
        {
            return;
        }

        var regionCode = _regions.ResolveRegionForLead(evt.LeadId);
        if (!_regions.TryGetPoint(regionCode, out var point))
        {
            return;
        }

        var value = payload.ValueUsd ?? 0m;
        var color = _colors.GetPrimaryColor(evt.Persona);
        var mapX = regionCode.StartsWith("us-", StringComparison.Ordinal) ? point.X : point.X;
        var mapY = regionCode.StartsWith("us-", StringComparison.Ordinal) ? point.Y : point.Y;

        _pins.Add(new MoneyMapPin(
            Guid.NewGuid(),
            evt.LeadId,
            evt.Persona,
            regionCode,
            point.Label,
            value,
            evt.OccurredAtUtc,
            mapX,
            mapY,
            color,
            IsNew: true));
    }

    public static double PinRadius(decimal valueUsd)
    {
        if (valueUsd <= 0)
        {
            return 6;
        }

        var scaled = Math.Sqrt((double)valueUsd / 10_000.0);
        return Math.Clamp(scaled + 4, 6, 28);
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
