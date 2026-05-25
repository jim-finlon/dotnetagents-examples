namespace SalesArena.HotStove;

/// <summary>
/// Scope of a between-contest training pass. Operators choose how aggressive
/// the Training Agent's prompt-variant generation is.
/// </summary>
public enum TrainingScope
{
    /// <summary>Single template tweak (low-risk).</summary>
    SingleTemplate = 1,
    /// <summary>Cadence + 1-2 templates (medium-risk).</summary>
    Moderate = 2,
    /// <summary>Multiple templates + cadence + decision-posture (high-risk).</summary>
    Aggressive = 3,
}

/// <summary>
/// One training pass output. The draft is NOT auto-promoted — promotion
/// requires <see cref="IPromotionDecider"/> to approve based on A/B
/// comparison in a subsequent contest.
/// </summary>
public sealed record TrainingDraft(
    string DraftId,
    string Persona,
    string SourceContestId,
    TrainingScope Scope,
    string DraftPromptText,
    string SourceVariantRef,
    DateTimeOffset DraftedAtUtc,
    bool IsPromoted = false,
    DateTimeOffset? PromotedAtUtc = null,
    double? AbDeltaScore = null);

/// <summary>
/// One operator-mediated template swap proposal. <see cref="OperatorApprovedAtUtc"/>
/// is null until the operator confirms; the trade does not apply until then.
/// </summary>
public sealed record TradeRequest(
    string TradeId,
    string PersonaA,
    string PersonaB,
    string TemplateRef,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? OperatorApprovedAtUtc,
    DateTimeOffset? AppliedAtUtc);

/// <summary>Ledger event-kind discriminators for the Hot Stove League surface.</summary>
public static class HotStoveEventKinds
{
    public const string PersonaTrainedAfterContest = "PersonaTrainedAfterContest";
    public const string TemplatesTraded = "TemplatesTraded";
    public const string TrainingDraftPromoted = "TrainingDraftPromoted";
    public const string TradeCooldownRejected = "TradeCooldownRejected";
}

public sealed record PersonaTrainedAfterContestPayload(
    string Persona,
    string SourceContestId,
    TrainingScope Scope,
    string DraftId,
    DateTimeOffset DraftedAtUtc);

public sealed record TemplatesTradedPayload(
    string TradeId,
    string PersonaA,
    string PersonaB,
    string TemplateRef,
    string OperatorId,
    DateTimeOffset AppliedAtUtc);

public sealed record TrainingDraftPromotedPayload(
    string DraftId,
    string Persona,
    double AbDeltaScore,
    DateTimeOffset PromotedAtUtc);
