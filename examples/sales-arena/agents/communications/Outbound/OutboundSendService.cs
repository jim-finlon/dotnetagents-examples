namespace SalesArena.Communications.Outbound;

/// <summary>
/// Demo-safe outbound pipeline: draft, governance, rate limit, preview gate, A/B send accounting.
/// </summary>
public sealed class OutboundSendService(
    IOutboundDrafter drafter,
    AbPromotionTracker promotionTracker,
    PreviewConfirmGate previewGate,
    SendRateLimiter rateLimiter)
{
    private readonly IOutboundDrafter _drafter = drafter ?? throw new ArgumentNullException(nameof(drafter));
    private readonly AbPromotionTracker _promotionTracker =
        promotionTracker ?? throw new ArgumentNullException(nameof(promotionTracker));
    private readonly PreviewConfirmGate _previewGate =
        previewGate ?? throw new ArgumentNullException(nameof(previewGate));
    private readonly SendRateLimiter _rateLimiter =
        rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));

    public async Task<OutboundSendResult> TrySendAsync(
        ProspectContext prospect,
        string personaId,
        string channel,
        OutboundIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prospect);
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);

        if (!OutreachGovernance.AllowsOutbound(prospect))
        {
            return new OutboundSendResult(
                OutboundSendStatus.GovernanceRefused,
                Detail: "Prospect has not opted in to outreach.");
        }

        var draft = await _drafter.DraftAsync(prospect, personaId, channel, intent, cancellationToken)
            .ConfigureAwait(false);

        if (_previewGate.RequiresPreview(personaId))
        {
            var preview = await _previewGate.StartPreviewAsync(draft, cancellationToken).ConfigureAwait(false);
            return new OutboundSendResult(
                OutboundSendStatus.PreviewRequired,
                draft,
                preview.SessionId,
                preview.ConfirmationToken);
        }

        if (!_rateLimiter.TryAcquire(personaId))
        {
            return new OutboundSendResult(
                OutboundSendStatus.RateLimited,
                Detail: $"Daily send cap ({_rateLimiter.DailyCap}) reached for persona '{personaId}'.");
        }

        _promotionTracker.RecordSend(personaId, channel, draft.VariantId);
        return new OutboundSendResult(OutboundSendStatus.Sent, draft);
    }

    public async Task<OutboundSendResult> ConfirmPreviewAndSendAsync(
        Guid previewSessionId,
        string confirmationToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationToken);

        var draft = await _previewGate.TryConfirmAndRecordSendAsync(
                previewSessionId,
                confirmationToken,
                cancellationToken)
            .ConfigureAwait(false);
        if (draft is null)
        {
            return new OutboundSendResult(
                OutboundSendStatus.PreviewNotConfirmed,
                Detail: "Preview session was not confirmed.");
        }

        if (!_rateLimiter.TryAcquire(draft.PersonaId))
        {
            return new OutboundSendResult(
                OutboundSendStatus.RateLimited,
                draft,
                Detail: $"Daily send cap ({_rateLimiter.DailyCap}) reached for persona '{draft.PersonaId}'.");
        }

        _promotionTracker.RecordSend(draft.PersonaId, draft.Channel, draft.VariantId);
        return new OutboundSendResult(OutboundSendStatus.Sent, draft);
    }
}
