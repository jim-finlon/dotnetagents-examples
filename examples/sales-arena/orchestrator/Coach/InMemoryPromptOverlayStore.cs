namespace SalesArena.Orchestrator.Coach;

/// <summary>
/// In-memory overlay store. One active overlay per persona at a time; a
/// new injection replaces (does not stack on) the existing one. Thread-safe
/// for the contest-tick cadence.
/// </summary>
public sealed class InMemoryPromptOverlayStore : IPromptOverlayStore
{
    private readonly CoachOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, PromptOverlay> _active = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public InMemoryPromptOverlayStore(CoachOptions? options = null, TimeProvider? timeProvider = null)
    {
        _options = options ?? new CoachOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public (PromptOverlay Overlay, CoachInterventionAppliedPayload LedgerPayload) Inject(
        string persona,
        string operatorId,
        string speech,
        int? expiresAfterTouches = null,
        DateTimeOffset? appliedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        if (string.IsNullOrEmpty(operatorId))
        {
            throw new CoachException(CoachErrorCode.OperatorRequired, "operatorId must be provided for an overlay");
        }
        var expires = expiresAfterTouches ?? _options.DefaultExpiresAfterTouches;
        if (expires <= 0)
        {
            throw new CoachException(CoachErrorCode.ExpiresAfterMustBePositive,
                "expiresAfterTouches must be positive (an overlay that expires immediately is meaningless)");
        }
        var sanitized = CoachSpeechSanitizer.Sanitize(speech, _options);
        var applied = appliedAtUtc ?? _timeProvider.GetUtcNow();

        var overlay = new PromptOverlay(
            Persona: persona,
            OperatorId: operatorId,
            SanitizedSpeech: sanitized,
            InitialTouches: expires,
            RemainingTouches: expires,
            AppliedAtUtc: applied,
            ExpiredAtUtc: null);

        lock (_lock)
        {
            _active[persona] = overlay;
        }

        var payload = new CoachInterventionAppliedPayload(
            Persona: persona,
            OperatorId: operatorId,
            SanitizedSpeech: sanitized,
            ExpiresAfterTouches: expires,
            AppliedAtUtc: applied);

        return (overlay, payload);
    }

    public PromptOverlay? GetActive(string persona)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        lock (_lock)
        {
            if (_active.TryGetValue(persona, out var overlay) && overlay.IsActive)
            {
                return overlay;
            }
            return null;
        }
    }

    public CoachInterventionExpiredPayload? ConsumeTouch(string persona, DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        lock (_lock)
        {
            if (!_active.TryGetValue(persona, out var overlay) || !overlay.IsActive)
            {
                return null;
            }
            var nextRemaining = overlay.RemainingTouches - 1;
            if (nextRemaining > 0)
            {
                _active[persona] = overlay with { RemainingTouches = nextRemaining };
                return null;
            }
            // This consumption tipped the counter to zero — expire.
            var expired = overlay with { RemainingTouches = 0, ExpiredAtUtc = nowUtc };
            _active[persona] = expired;
            return new CoachInterventionExpiredPayload(
                Persona: persona,
                OperatorId: overlay.OperatorId,
                TouchesConsumed: overlay.InitialTouches,
                AppliedAtUtc: overlay.AppliedAtUtc,
                ExpiredAtUtc: nowUtc);
        }
    }
}
