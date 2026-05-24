namespace SalesArena.Manager.Web.Services.ContestSettings;

/// <summary>
/// Manager UI seam for contest init/start until SA-02-05 lands in orchestrator.
/// </summary>
public interface IContestLifecycleHost
{
    ContestRunState State { get; }

    string? LastStatusMessage { get; }

    Task<ContestLifecycleResult> InitAsync(ContestSettingsDraft draft, CancellationToken cancellationToken = default);

    Task<ContestLifecycleResult> StartAsync(CancellationToken cancellationToken = default);
}
