namespace SalesArena.HotStove;

/// <summary>
/// Deterministic test agent. Produces a draft variant string keyed on the
/// inputs so that two calls with identical args produce identical drafts
/// (the auditability invariant pinned by tests).
/// </summary>
public sealed class StubTrainingAgent : ITrainingAgent
{
    public Task<TrainingDraft> ProposeDraftAsync(
        string persona,
        string contestId,
        TrainingScope scope,
        string sourceVariantRef,
        DateTimeOffset draftedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        ArgumentException.ThrowIfNullOrEmpty(contestId);
        ArgumentException.ThrowIfNullOrEmpty(sourceVariantRef);

        var draft = new TrainingDraft(
            DraftId: Guid.NewGuid().ToString("N"),
            Persona: persona,
            SourceContestId: contestId,
            Scope: scope,
            DraftPromptText: $"[stub-{scope.ToString().ToLowerInvariant()}] derived-from:{sourceVariantRef} for {persona} in {contestId}",
            SourceVariantRef: sourceVariantRef,
            DraftedAtUtc: draftedAtUtc);
        return Task.FromResult(draft);
    }
}
