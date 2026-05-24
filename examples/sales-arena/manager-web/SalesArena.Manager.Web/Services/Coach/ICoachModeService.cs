using SalesArena.Orchestrator.Coach;

namespace SalesArena.Manager.Web.Services.Coach;

public interface ICoachModeService
{
    CoachPreviewResult TryBuildPreview(string speech, string basePromptStub);

    CoachApplyResult Apply(string persona, string operatorId, string speech, int expiresAfterTouches = 10);

    PromptOverlay? GetActive(string persona);

    void ClearOverlay(string persona);
}

public sealed record CoachPreviewResult(
    bool Success,
    CoachErrorCode? ErrorCode,
    string? ComposedPromptPreview,
    string? SanitizedSpeech,
    string? ErrorMessage);

public sealed record CoachApplyResult(
    bool Success,
    CoachErrorCode? ErrorCode,
    PromptOverlay? Overlay,
    string? ErrorMessage);
