namespace SalesArena.HotStove;

/// <summary>
/// Generates a draft prompt variant from a contest's outcome + the
/// persona's existing prompt. Production hosts plug the SA-01-08 Training
/// Agent; tests + offline replay use <see cref="StubTrainingAgent"/>.
///
/// <para>Story SA-08-18 constraint: training is non-destructive — produces
/// drafts. Drafts only become live prompt variants when promoted via
/// <see cref="IPromotionDecider"/>.</para>
/// </summary>
public interface ITrainingAgent
{
    Task<TrainingDraft> ProposeDraftAsync(
        string persona,
        string contestId,
        TrainingScope scope,
        string sourceVariantRef,
        DateTimeOffset draftedAtUtc,
        CancellationToken cancellationToken = default);
}
