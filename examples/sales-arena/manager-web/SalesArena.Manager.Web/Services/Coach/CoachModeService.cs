using SalesArena.Orchestrator.Coach;

namespace SalesArena.Manager.Web.Services.Coach;

/// <summary>
/// Manager-web façade over <see cref="IPromptOverlayStore"/> — preview uses the
/// same sanitizer/applier as the orchestrator (no duplicate rules).
/// </summary>
public sealed class CoachModeService : ICoachModeService
{
    private const string DefaultBasePromptStub = "(persona base system prompt)";

    private readonly IPromptOverlayStore _store;
    private readonly CoachOptions _options;
    private readonly TimeProvider _timeProvider;

    public CoachModeService(
        IPromptOverlayStore store,
        CoachOptions options,
        TimeProvider? timeProvider = null)
    {
        _store = store;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public CoachPreviewResult TryBuildPreview(string speech, string basePromptStub)
    {
        try
        {
            var sanitized = CoachSpeechSanitizer.Sanitize(speech, _options);
            var overlay = new PromptOverlay(
                Persona: "_preview",
                OperatorId: "_preview",
                SanitizedSpeech: sanitized,
                InitialTouches: _options.DefaultExpiresAfterTouches,
                RemainingTouches: _options.DefaultExpiresAfterTouches,
                AppliedAtUtc: _timeProvider.GetUtcNow(),
                ExpiredAtUtc: null);
            var composed = PromptOverlayApplier.Compose(
                string.IsNullOrWhiteSpace(basePromptStub) ? DefaultBasePromptStub : basePromptStub,
                overlay);
            return new CoachPreviewResult(true, null, composed, sanitized, null);
        }
        catch (CoachException ex)
        {
            return new CoachPreviewResult(false, ex.Code, null, null, ex.Message);
        }
    }

    public CoachApplyResult Apply(string persona, string operatorId, string speech, int expiresAfterTouches = 10)
    {
        try
        {
            var (overlay, _) = _store.Inject(persona, operatorId, speech, expiresAfterTouches);
            return new CoachApplyResult(true, null, overlay, null);
        }
        catch (CoachException ex)
        {
            return new CoachApplyResult(false, ex.Code, null, ex.Message);
        }
    }

    public PromptOverlay? GetActive(string persona) => _store.GetActive(persona);

    public void ClearOverlay(string persona)
    {
        // Early expiry: consume touches until the overlay expires.
        for (var i = 0; i < 512; i++)
        {
            var expired = _store.ConsumeTouch(persona, _timeProvider.GetUtcNow());
            if (expired is not null || _store.GetActive(persona) is null)
            {
                return;
            }
        }
    }
}
