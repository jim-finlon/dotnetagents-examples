namespace SalesArena.Orchestrator.LeadPool;

/// <summary>
/// The lead pool. Loads a lead pack from JSON, distributes leads to persona
/// pods atomically (no double-claims), and supports tier-aware filtering for
/// the Glengarry-drip mechanic (SA-02-07).
/// </summary>
public interface ILeadPool
{
    /// <summary>Load a lead pack from JSON. Returns the loaded handle for fluent use.</summary>
    /// <exception cref="LeadPoolException">Code <c>LEAD_POOL_PACK_INVALID</c> when the JSON is malformed or fails schema invariants.</exception>
    Task<LeadPack> LoadAsync(string packJsonPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claim <paramref name="count"/> available leads for the given pod.
    /// Returns the claimed leads (oldest-loaded first). Refuses to claim already-
    /// assigned or already-released leads.
    /// </summary>
    /// <param name="podId">Persona pod id (e.g. "roma", "levene").</param>
    /// <param name="count">How many leads to claim.</param>
    /// <param name="tier">Optional tier filter ("glengarry" / "cold"). Null = either tier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="LeadPoolException">
    /// Code <c>LEAD_POOL_NOT_LOADED</c> when no pack has been loaded.<br/>
    /// Code <c>LEAD_POOL_INSUFFICIENT_AVAILABLE</c> when fewer than <paramref name="count"/> matching leads are available.
    /// </exception>
    Task<IReadOnlyList<Lead>> AssignAsync(string podId, int count, string? tier = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Release leads back to the pool. The supplied leads must be currently
    /// assigned to <paramref name="podId"/> (no cross-pod releases). Released
    /// leads become re-assignable to any pod.
    /// </summary>
    /// <exception cref="LeadPoolException">
    /// Code <c>LEAD_POOL_LEAD_UNKNOWN</c> when a lead id is not in the pack.<br/>
    /// Code <c>LEAD_POOL_NOT_ASSIGNED_TO_POD</c> when a lead is not currently assigned to <paramref name="podId"/>.
    /// </exception>
    Task ReleaseAsync(string podId, IEnumerable<string> leadIds, CancellationToken cancellationToken = default);

    /// <summary>Read-only stats snapshot.</summary>
    LeadPoolSnapshot Snapshot();

    /// <summary>Which pod currently owns this lead, or null if it is available/released.</summary>
    string? GetAssignedPod(string leadId);

    /// <summary>
    /// Lead ids currently assigned to <paramref name="podId"/>. Returns the
    /// snapshot in load-order; safe to call concurrently with Assign/Release
    /// (the result is a defensive copy).
    /// </summary>
    IReadOnlyList<string> GetAssignedLeads(string podId);
}
