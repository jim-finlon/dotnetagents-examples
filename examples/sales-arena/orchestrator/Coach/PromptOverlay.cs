namespace SalesArena.Orchestrator.Coach;

/// <summary>
/// One operator-injected "halftime speech" attached to a persona. Carries the
/// sanitized text + a touch-count counter that ticks down on each touch the
/// orchestrator emits; at zero the overlay expires and the persona reverts
/// to base prompt. The original (un-sanitized) speech is NOT stored — only
/// the sanitized text reaches the ledger or the prompt assembly.
/// </summary>
public sealed record PromptOverlay(
    string Persona,
    string OperatorId,
    string SanitizedSpeech,
    int InitialTouches,
    int RemainingTouches,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset? ExpiredAtUtc)
{
    public bool IsActive => RemainingTouches > 0 && ExpiredAtUtc is null;
}

/// <summary>
/// Ledger payload emitted when an overlay is applied. Mirrors
/// <see cref="CoachEventKinds.CoachInterventionApplied"/>.
/// </summary>
public sealed record CoachInterventionAppliedPayload(
    string Persona,
    string OperatorId,
    string SanitizedSpeech,
    int ExpiresAfterTouches,
    DateTimeOffset AppliedAtUtc);

/// <summary>Ledger payload emitted when an overlay expires (counter hits zero).</summary>
public sealed record CoachInterventionExpiredPayload(
    string Persona,
    string OperatorId,
    int TouchesConsumed,
    DateTimeOffset AppliedAtUtc,
    DateTimeOffset ExpiredAtUtc);
