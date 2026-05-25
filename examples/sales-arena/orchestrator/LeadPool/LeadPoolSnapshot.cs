namespace SalesArena.Orchestrator.LeadPool;

/// <summary>
/// Read-only stats snapshot for the Manager UI Lead-Pool view (SA-03-04) and
/// the leaderboard inputs (SA-02-04). The numbers are mutually exclusive:
/// every lead is in exactly one of Available / Assigned / Released, and
/// Total = Available + Assigned + Released.
/// </summary>
public sealed record LeadPoolSnapshot(
    int Total,
    int Available,
    int Assigned,
    int Released,
    IReadOnlyDictionary<string, int> AssignedByPod,
    IReadOnlyDictionary<string, int> AvailableByTier);
