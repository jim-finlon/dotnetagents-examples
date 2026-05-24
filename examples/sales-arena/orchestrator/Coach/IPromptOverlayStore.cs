namespace SalesArena.Orchestrator.Coach;

/// <summary>
/// Tracks active prompt overlays per persona. The orchestrator queries this
/// on every touch to assemble the per-touch system prompt (base prompt +
/// active overlay's sanitized speech) and to tick the counter down.
/// </summary>
public interface IPromptOverlayStore
{
    /// <summary>
    /// Inject a new overlay for <paramref name="persona"/>. Replaces any
    /// existing active overlay for the same persona (the new halftime
    /// speech supersedes the old).
    /// </summary>
    /// <returns>The persisted overlay record + the ledger payload to emit.</returns>
    (PromptOverlay Overlay, CoachInterventionAppliedPayload LedgerPayload) Inject(
        string persona,
        string operatorId,
        string speech,
        int? expiresAfterTouches = null,
        DateTimeOffset? appliedAtUtc = null);

    /// <summary>Active overlay for <paramref name="persona"/>, or null.</summary>
    PromptOverlay? GetActive(string persona);

    /// <summary>
    /// Consume one touch's worth of the overlay's counter. Returns the
    /// expired-overlay payload if this consumption tipped it past zero
    /// (caller emits <see cref="CoachEventKinds.CoachInterventionExpired"/>).
    /// </summary>
    CoachInterventionExpiredPayload? ConsumeTouch(string persona, DateTimeOffset nowUtc);
}
