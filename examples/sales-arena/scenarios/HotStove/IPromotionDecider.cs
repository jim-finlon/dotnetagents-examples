namespace SalesArena.HotStove;

/// <summary>
/// Decides whether a training draft should be promoted to a live prompt
/// variant. The default <see cref="DefaultAbPromotionDecider"/> checks the
/// caller-supplied A/B delta against <see cref="HotStoveOptions.DefaultAbPromotionFloor"/>;
/// production hosts can plug a stricter (e.g. require statistically-significant
/// delta) or more permissive (operator-only approval) decider.
/// </summary>
public interface IPromotionDecider
{
    bool ShouldPromote(TrainingDraft draft, double abDeltaScore);
}

public sealed class DefaultAbPromotionDecider : IPromotionDecider
{
    private readonly double _floor;

    public DefaultAbPromotionDecider(HotStoveOptions? options = null)
    {
        _floor = (options ?? new HotStoveOptions()).DefaultAbPromotionFloor;
    }

    public bool ShouldPromote(TrainingDraft draft, double abDeltaScore)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.IsPromoted) return false;
        return abDeltaScore > _floor;
    }
}
