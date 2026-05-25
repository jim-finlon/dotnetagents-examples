namespace SalesArena.Manager.Web.Services.ContestSettings;

/// <summary>
/// In-memory contest lifecycle for the Settings page demo path (no ledger writes).
/// </summary>
public sealed class DemoContestLifecycleHost : IContestLifecycleHost
{
    private readonly object _gate = new();
    private ContestRunState _state = ContestRunState.Idle;
    private string? _lastStatusMessage;

    public ContestRunState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public string? LastStatusMessage
    {
        get
        {
            lock (_gate)
            {
                return _lastStatusMessage;
            }
        }
    }

    public Task<ContestLifecycleResult> InitAsync(
        ContestSettingsDraft draft,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_state is ContestRunState.Active or ContestRunState.Paused)
            {
                return Task.FromResult(ContestLifecycleResult.Blocked(
                    "An active contest is running. Pause it before applying new settings."));
            }

            if (draft.EnabledPersonas.Count == 0)
            {
                return Task.FromResult(ContestLifecycleResult.Blocked(
                    ContestSettingsValidation.PersonaRequiredMessage));
            }

            _state = ContestRunState.Initialized;
            _lastStatusMessage =
                $"Initialized '{draft.ContestName}' ({draft.DurationHours}h, {draft.EnabledPersonas.Count} personas, metric {draft.ScoringMetricId}).";
            return Task.FromResult(ContestLifecycleResult.Ok(_lastStatusMessage));
        }
    }

    public Task<ContestLifecycleResult> StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_state == ContestRunState.Active)
            {
                return Task.FromResult(ContestLifecycleResult.Blocked("Contest is already active."));
            }

            if (_state != ContestRunState.Initialized)
            {
                return Task.FromResult(ContestLifecycleResult.Blocked(
                    "Initialize contest settings before starting."));
            }

            _state = ContestRunState.Active;
            _lastStatusMessage = "Contest started (demo host — wire SA-02-05 for ledger-backed lifecycle).";
            return Task.FromResult(ContestLifecycleResult.Ok(_lastStatusMessage));
        }
    }
}
