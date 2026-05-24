namespace SalesArena.Crm;

/// <summary>
/// Append-only per-lead activity log. Records every transition + touch + inbound.
/// The replay engine (SA-04-01) reads this for the drill-down view.
/// </summary>
public interface IActivityLog : IAsyncDisposable
{
    /// <summary>Append a single entry. Returns the assigned id.</summary>
    Task<long> AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Read every entry for one lead, oldest-first.</summary>
    Task<IReadOnlyList<ActivityLogEntry>> GetByLeadAsync(string leadId, CancellationToken cancellationToken = default);

    /// <summary>Total entry count across all leads (used for stats + tests).</summary>
    Task<long> CountAsync(CancellationToken cancellationToken = default);
}
