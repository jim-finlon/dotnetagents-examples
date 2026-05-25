using SalesArena.Manager.Web.Services.ContestSettings;

namespace SalesArena.Manager.Web.Services.BossOffice;

/// <summary>
/// Demo metrics for the Boss Office until orchestrator cost telemetry (SA-02+) is wired.
/// </summary>
public sealed class DemoBossOfficeMetricsProvider : IBossOfficeMetricsProvider
{
    private readonly BossOfficeCostCatalog _catalog;
    private readonly IContestLifecycleHost _contestLifecycle;
    private readonly object _gate = new();
    private BossOfficeMetricsSnapshot _snapshot;

    public DemoBossOfficeMetricsProvider(
        BossOfficeCostCatalogLoader catalogLoader,
        IContestLifecycleHost contestLifecycle)
    {
        _catalog = catalogLoader.Load();
        _contestLifecycle = contestLifecycle;
        _snapshot = BuildSnapshot();
    }

    public BossOfficeMetricsSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    public void Refresh()
    {
        lock (_gate)
        {
            _snapshot = BuildSnapshot();
        }
    }

    private BossOfficeMetricsSnapshot BuildSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var contestActive = _contestLifecycle.State is ContestRunState.Active or ContestRunState.Paused;
        var velocity = contestActive ? 42.5m : 12.0m;
        var saturation = contestActive ? 68.4m : 24.0m;
        var touches = 1280;
        var revenue = 186_400m;
        var spend = touches * _catalog.CostPerTouchUsd + _catalog.ModelTierSpend.Sum(t => t.SpendUsd);
        var roi = revenue - spend;

        var trend = new List<CostPerTouchPoint>(capacity: 6);
        for (var i = 5; i >= 0; i--)
        {
            var hour = now.AddHours(-i);
            var jitter = (decimal)(i * 0.01);
            trend.Add(new CostPerTouchPoint(hour, _catalog.CostPerTouchUsd + jitter));
        }

        var personaCosts = new List<PersonaOpportunityCost>
        {
            new("romano", "Romano", 4200m),
            new("moss", "Moss", 3100m),
            new("levene", "Levene", 5400m),
            new("aaronow", "Aaronow", 2800m),
            new("williamson", "Williamson", 3900m),
            new("harris", "Harris", 2200m),
        };

        return new BossOfficeMetricsSnapshot(
            _catalog.AsOfUtc,
            now,
            roi,
            _catalog.CostPerTouchUsd,
            trend,
            _catalog.ModelTierSpend,
            personaCosts,
            velocity,
            saturation);
    }
}
