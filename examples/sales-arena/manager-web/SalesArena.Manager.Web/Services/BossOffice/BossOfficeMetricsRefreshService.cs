namespace SalesArena.Manager.Web.Services.BossOffice;

/// <summary>
/// Refreshes demo Boss Office metrics on a five-second cadence.
/// </summary>
public sealed class BossOfficeMetricsRefreshService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private readonly DemoBossOfficeMetricsProvider _provider;

    public BossOfficeMetricsRefreshService(IBossOfficeMetricsProvider provider)
    {
        _provider = provider as DemoBossOfficeMetricsProvider
            ?? throw new InvalidOperationException("Boss Office refresh requires DemoBossOfficeMetricsProvider.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            _provider.Refresh();
        }
    }
}
