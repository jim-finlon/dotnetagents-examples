using System.Text.Json;
using DotNetAgents.PreviewConfirm;

namespace SalesArena.Communications.Outbound;

/// <summary>
/// Requires operator preview-and-confirm for the first N sends per persona (default 25).
/// Preview cannot be disabled without an explicit operator flag; rate limits remain enforced separately.
/// </summary>
public sealed class PreviewConfirmGate
{
    public const int DefaultPreviewSendThreshold = 25;

    private readonly PreviewConfirmCoordinator _coordinator;
    private readonly int _previewThreshold;
    private readonly bool _operatorPreviewDisabled;
    private readonly Dictionary<string, int> _confirmedSendCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, PendingPreview> _pending = new();
    private readonly Lock _lock = new();

    public PreviewConfirmGate(
        PreviewConfirmCoordinator coordinator,
        int previewThreshold = DefaultPreviewSendThreshold,
        bool operatorPreviewDisabled = false)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(previewThreshold);
        _previewThreshold = previewThreshold;
        _operatorPreviewDisabled = operatorPreviewDisabled;
    }

    public bool RequiresPreview(string personaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        if (_operatorPreviewDisabled)
        {
            return false;
        }

        lock (_lock)
        {
            _confirmedSendCounts.TryGetValue(personaId, out var count);
            return count < _previewThreshold;
        }
    }

    public int GetConfirmedSendCount(string personaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        lock (_lock)
        {
            return _confirmedSendCounts.GetValueOrDefault(personaId);
        }
    }

    public async Task<PreviewConfirmStartResult> StartPreviewAsync(
        DraftMessage draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var payload = SerializeDraft(draft);
        var started = await _coordinator.StartPreviewAsync(payload, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        lock (_lock)
        {
            _pending[started.SessionId] = new PendingPreview(draft.PersonaId, draft.Channel, draft.VariantId);
        }

        return started;
    }

    public async Task<DraftMessage?> TryConfirmAndRecordSendAsync(
        Guid sessionId,
        string confirmationToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationToken);

        var result = await _coordinator.ConfirmAsync(sessionId, confirmationToken, cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success || result.Session is null)
        {
            return null;
        }

        PendingPreview pending;
        lock (_lock)
        {
            if (!_pending.Remove(sessionId, out pending))
            {
                pending = ParsePending(result.Session.PreviewPayload);
            }

            _confirmedSendCounts.TryGetValue(pending.PersonaId, out var count);
            _confirmedSendCounts[pending.PersonaId] = count + 1;
        }

        return ParseDraft(result.Session.PreviewPayload);
    }

    private static PendingPreview ParsePending(string payload)
    {
        var draft = ParseDraft(payload);
        return new PendingPreview(draft.PersonaId, draft.Channel, draft.VariantId);
    }

    internal static DraftMessage ParseDraft(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        var dto = JsonSerializer.Deserialize<DraftPayloadDto>(payload)
            ?? throw new FormatException("Invalid preview payload.");
        return new DraftMessage(
            dto.PersonaId,
            dto.Channel,
            dto.VariantId,
            dto.Subject,
            dto.Body,
            dto.Confidence,
            dto.Hypothesis);
    }

    private static string SerializeDraft(DraftMessage draft) =>
        JsonSerializer.Serialize(
            new DraftPayloadDto(
                draft.PersonaId,
                draft.Channel,
                draft.VariantId,
                draft.Subject,
                draft.Body,
                draft.Confidence,
                draft.Hypothesis));

    private sealed record DraftPayloadDto(
        string PersonaId,
        string Channel,
        string VariantId,
        string? Subject,
        string Body,
        double Confidence,
        string Hypothesis);

    private readonly record struct PendingPreview(string PersonaId, string Channel, string VariantId);
}
