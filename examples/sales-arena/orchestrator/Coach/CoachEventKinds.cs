namespace SalesArena.Orchestrator.Coach;

/// <summary>
/// Ledger event discriminators emitted by Coach Mode. Strings so the
/// orchestrator's ArenaEventKinds can absorb them in a follow-up slice
/// without forcing a cross-package coupling now (same pattern used by
/// SA-08-17 The Bench's BenchEventKinds).
/// </summary>
public static class CoachEventKinds
{
    public const string CoachInterventionApplied = "CoachInterventionApplied";
    public const string CoachInterventionExpired = "CoachInterventionExpired";
}
