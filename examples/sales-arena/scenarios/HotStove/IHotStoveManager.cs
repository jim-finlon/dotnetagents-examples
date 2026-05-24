namespace SalesArena.HotStove;

/// <summary>
/// Between-contest persona ops. Two flows: training (non-destructive draft
/// generation that may be promoted later) and trading (operator-mediated
/// template swap with cooldown enforcement).
/// </summary>
public interface IHotStoveManager
{
    Task<(TrainingDraft Draft, PersonaTrainedAfterContestPayload LedgerPayload)> TrainAsync(
        string persona,
        string contestId,
        TrainingScope scope,
        string sourceVariantRef,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// File a trade request between two personas. The trade is NOT applied
    /// until <see cref="ApproveTradeAsync"/> is called by the operator.
    /// Cooldown is enforced at request time so an operator cannot file a
    /// request while a previous trade is still on cooldown.
    /// </summary>
    TradeRequest RequestTrade(string personaA, string personaB, string templateRef, DateTimeOffset nowUtc);

    /// <summary>
    /// Operator-confirmed trade application. Emits a TemplatesTraded ledger
    /// payload and sets both personas' last-trade timestamp for cooldown.
    /// </summary>
    TemplatesTradedPayload ApproveTrade(string tradeId, string operatorId, DateTimeOffset nowUtc);

    /// <summary>
    /// Evaluate a draft against an A/B delta score and promote when the
    /// configured decider approves. Returns the payload to emit when
    /// promotion fires; null otherwise.
    /// </summary>
    TrainingDraftPromotedPayload? EvaluateDraftForPromotion(string draftId, double abDeltaScore, DateTimeOffset nowUtc);

    TrainingDraft? GetDraft(string draftId);
    TradeRequest? GetTrade(string tradeId);
    DateTimeOffset? LastTradeAt(string persona);
}
