namespace SalesArena.HotStove;

public sealed class InMemoryHotStoveManager : IHotStoveManager
{
    private readonly HotStoveOptions _options;
    private readonly ITrainingAgent _trainingAgent;
    private readonly IPromotionDecider _promoter;
    private readonly TimeProvider _timeProvider;

    private readonly Dictionary<string, TrainingDraft> _drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TradeRequest> _trades = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastTradeAt = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public InMemoryHotStoveManager(
        ITrainingAgent trainingAgent,
        IPromotionDecider? promoter = null,
        HotStoveOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _trainingAgent = trainingAgent ?? throw new ArgumentNullException(nameof(trainingAgent));
        _options = options ?? new HotStoveOptions();
        _promoter = promoter ?? new DefaultAbPromotionDecider(_options);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<(TrainingDraft Draft, PersonaTrainedAfterContestPayload LedgerPayload)> TrainAsync(
        string persona,
        string contestId,
        TrainingScope scope,
        string sourceVariantRef,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        if (string.IsNullOrEmpty(contestId))
        {
            throw new HotStoveException(HotStoveErrorCode.EmptyContestId,
                "contestId is required so the draft can be traced to the source contest");
        }
        if (string.IsNullOrEmpty(sourceVariantRef))
        {
            throw new HotStoveException(HotStoveErrorCode.EmptyTemplateRef,
                "sourceVariantRef is required so the draft can be A/B-compared to a known baseline");
        }

        var draftedAt = _timeProvider.GetUtcNow();
        var draft = await _trainingAgent.ProposeDraftAsync(persona, contestId, scope, sourceVariantRef, draftedAt, cancellationToken).ConfigureAwait(false);
        lock (_lock)
        {
            _drafts[draft.DraftId] = draft;
        }
        var payload = new PersonaTrainedAfterContestPayload(
            Persona: persona,
            SourceContestId: contestId,
            Scope: scope,
            DraftId: draft.DraftId,
            DraftedAtUtc: draftedAt);
        return (draft, payload);
    }

    public TradeRequest RequestTrade(string personaA, string personaB, string templateRef, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(personaA);
        ArgumentException.ThrowIfNullOrEmpty(personaB);
        if (string.IsNullOrEmpty(templateRef))
        {
            throw new HotStoveException(HotStoveErrorCode.EmptyTemplateRef, "templateRef is required");
        }
        if (string.Equals(personaA, personaB, StringComparison.Ordinal))
        {
            throw new HotStoveException(HotStoveErrorCode.SamePersonaTrade,
                "trade requires two distinct personas");
        }

        lock (_lock)
        {
            EnforceCooldown(personaA, nowUtc);
            EnforceCooldown(personaB, nowUtc);

            var request = new TradeRequest(
                TradeId: Guid.NewGuid().ToString("N"),
                PersonaA: personaA,
                PersonaB: personaB,
                TemplateRef: templateRef,
                RequestedAtUtc: nowUtc,
                OperatorApprovedAtUtc: null,
                AppliedAtUtc: null);
            _trades[request.TradeId] = request;
            return request;
        }
    }

    public TemplatesTradedPayload ApproveTrade(string tradeId, string operatorId, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(tradeId);
        if (string.IsNullOrEmpty(operatorId))
        {
            throw new HotStoveException(HotStoveErrorCode.OperatorApprovalRequired,
                "operatorId is required to approve a trade");
        }
        lock (_lock)
        {
            if (!_trades.TryGetValue(tradeId, out var trade))
            {
                throw new HotStoveException(HotStoveErrorCode.UnknownTrade, $"unknown trade '{tradeId}'");
            }
            if (trade.AppliedAtUtc is not null)
            {
                throw new HotStoveException(HotStoveErrorCode.TradeAlreadyApplied,
                    $"trade '{tradeId}' was already applied at {trade.AppliedAtUtc}");
            }

            var applied = trade with
            {
                OperatorApprovedAtUtc = nowUtc,
                AppliedAtUtc = nowUtc,
            };
            _trades[tradeId] = applied;
            _lastTradeAt[trade.PersonaA] = nowUtc;
            _lastTradeAt[trade.PersonaB] = nowUtc;

            return new TemplatesTradedPayload(
                TradeId: tradeId,
                PersonaA: trade.PersonaA,
                PersonaB: trade.PersonaB,
                TemplateRef: trade.TemplateRef,
                OperatorId: operatorId,
                AppliedAtUtc: nowUtc);
        }
    }

    public TrainingDraftPromotedPayload? EvaluateDraftForPromotion(string draftId, double abDeltaScore, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(draftId);
        lock (_lock)
        {
            if (!_drafts.TryGetValue(draftId, out var draft))
            {
                throw new HotStoveException(HotStoveErrorCode.UnknownDraft, $"unknown draft '{draftId}'");
            }
            if (draft.IsPromoted)
            {
                throw new HotStoveException(HotStoveErrorCode.DraftAlreadyPromoted,
                    $"draft '{draftId}' has already been promoted at {draft.PromotedAtUtc}");
            }
            if (!_promoter.ShouldPromote(draft, abDeltaScore))
            {
                _drafts[draftId] = draft with { AbDeltaScore = abDeltaScore };
                return null;
            }

            var promoted = draft with
            {
                IsPromoted = true,
                PromotedAtUtc = nowUtc,
                AbDeltaScore = abDeltaScore,
            };
            _drafts[draftId] = promoted;
            return new TrainingDraftPromotedPayload(
                DraftId: draftId,
                Persona: draft.Persona,
                AbDeltaScore: abDeltaScore,
                PromotedAtUtc: nowUtc);
        }
    }

    public TrainingDraft? GetDraft(string draftId)
    {
        ArgumentException.ThrowIfNullOrEmpty(draftId);
        lock (_lock) return _drafts.TryGetValue(draftId, out var d) ? d : null;
    }

    public TradeRequest? GetTrade(string tradeId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tradeId);
        lock (_lock) return _trades.TryGetValue(tradeId, out var t) ? t : null;
    }

    public DateTimeOffset? LastTradeAt(string persona)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        lock (_lock) return _lastTradeAt.TryGetValue(persona, out var t) ? t : null;
    }

    private void EnforceCooldown(string persona, DateTimeOffset nowUtc)
    {
        if (_lastTradeAt.TryGetValue(persona, out var last))
        {
            var elapsed = nowUtc - last;
            if (elapsed < _options.TradeCooldown)
            {
                var remaining = _options.TradeCooldown - elapsed;
                throw new HotStoveException(HotStoveErrorCode.TradeCooldownActive,
                    $"persona '{persona}' last traded {elapsed.TotalHours:F1}h ago; cooldown leaves {remaining.TotalHours:F1}h");
            }
        }
    }
}
