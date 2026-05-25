using FluentAssertions;
using SalesArena.HotStove;
using Xunit;

namespace SalesArena.HotStove.Tests;

public sealed class InMemoryHotStoveManagerTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static InMemoryHotStoveManager Build(
        out FakeTimeProvider time,
        HotStoveOptions? options = null,
        IPromotionDecider? promoter = null)
    {
        time = new FakeTimeProvider(_t0);
        return new InMemoryHotStoveManager(
            trainingAgent: new StubTrainingAgent(),
            promoter: promoter,
            options: options,
            timeProvider: time);
    }

    [Fact]
    public async Task TrainAsync_produces_draft_that_is_not_auto_promoted()
    {
        var mgr = Build(out _);
        var (draft, payload) = await mgr.TrainAsync(
            persona: "roma",
            contestId: "contest-1",
            scope: TrainingScope.Moderate,
            sourceVariantRef: "templates/roma/variant-1");

        draft.IsPromoted.Should().BeFalse();
        draft.Persona.Should().Be("roma");
        draft.SourceContestId.Should().Be("contest-1");
        draft.Scope.Should().Be(TrainingScope.Moderate);
        draft.SourceVariantRef.Should().Be("templates/roma/variant-1");
        draft.DraftPromptText.Should().NotBeNullOrEmpty();

        payload.Persona.Should().Be("roma");
        payload.DraftId.Should().Be(draft.DraftId);

        mgr.GetDraft(draft.DraftId).Should().Be(draft);
    }

    [Fact]
    public async Task TrainAsync_requires_contest_id_and_source_variant()
    {
        var mgr = Build(out _);
        Func<Task> noContest = () => mgr.TrainAsync("roma", "", TrainingScope.SingleTemplate, "v1");
        Func<Task> noVariant = () => mgr.TrainAsync("roma", "c-1", TrainingScope.SingleTemplate, "");
        (await noContest.Should().ThrowAsync<HotStoveException>()).Subject.First().Code.Should().Be(HotStoveErrorCode.EmptyContestId);
        (await noVariant.Should().ThrowAsync<HotStoveException>()).Subject.First().Code.Should().Be(HotStoveErrorCode.EmptyTemplateRef);
    }

    [Fact]
    public void RequestTrade_creates_pending_request_without_applying()
    {
        var mgr = Build(out _);
        var trade = mgr.RequestTrade("roma", "levene", "templates/closer-script", _t0);

        trade.PersonaA.Should().Be("roma");
        trade.PersonaB.Should().Be("levene");
        trade.TemplateRef.Should().Be("templates/closer-script");
        trade.RequestedAtUtc.Should().Be(_t0);
        trade.OperatorApprovedAtUtc.Should().BeNull("operator must approve before apply");
        trade.AppliedAtUtc.Should().BeNull();
        mgr.GetTrade(trade.TradeId).Should().Be(trade);
    }

    [Fact]
    public void RequestTrade_refuses_same_persona_on_both_sides()
    {
        var mgr = Build(out _);
        Action act = () => mgr.RequestTrade("roma", "roma", "x", _t0);
        act.Should().Throw<HotStoveException>().Which.Code.Should().Be(HotStoveErrorCode.SamePersonaTrade);
    }

    [Fact]
    public void RequestTrade_refuses_empty_templateRef()
    {
        var mgr = Build(out _);
        Action act = () => mgr.RequestTrade("roma", "levene", "", _t0);
        act.Should().Throw<HotStoveException>().Which.Code.Should().Be(HotStoveErrorCode.EmptyTemplateRef);
    }

    [Fact]
    public void ApproveTrade_requires_operatorId()
    {
        var mgr = Build(out _);
        var trade = mgr.RequestTrade("roma", "levene", "x", _t0);
        Action act = () => mgr.ApproveTrade(trade.TradeId, "", _t0.AddMinutes(1));
        act.Should().Throw<HotStoveException>().Which.Code.Should().Be(HotStoveErrorCode.OperatorApprovalRequired);
    }

    [Fact]
    public void ApproveTrade_applies_trade_and_emits_payload_with_both_personas_on_cooldown()
    {
        var mgr = Build(out _);
        var trade = mgr.RequestTrade("roma", "levene", "templates/x", _t0);
        var payload = mgr.ApproveTrade(trade.TradeId, "jim", _t0.AddMinutes(2));

        payload.TradeId.Should().Be(trade.TradeId);
        payload.OperatorId.Should().Be("jim");
        payload.PersonaA.Should().Be("roma");
        payload.PersonaB.Should().Be("levene");
        payload.AppliedAtUtc.Should().Be(_t0.AddMinutes(2));

        var stored = mgr.GetTrade(trade.TradeId)!;
        stored.AppliedAtUtc.Should().Be(_t0.AddMinutes(2));
        stored.OperatorApprovedAtUtc.Should().Be(_t0.AddMinutes(2));

        mgr.LastTradeAt("roma").Should().Be(_t0.AddMinutes(2));
        mgr.LastTradeAt("levene").Should().Be(_t0.AddMinutes(2));
    }

    [Fact]
    public void RequestTrade_enforces_48h_cooldown_after_approval()
    {
        var mgr = Build(out var time);
        var first = mgr.RequestTrade("roma", "levene", "templates/x", _t0);
        mgr.ApproveTrade(first.TradeId, "jim", _t0.AddMinutes(1));

        // Try another trade involving roma 1 hour later — should refuse.
        time.Advance(TimeSpan.FromHours(1));
        Action withinCooldown = () => mgr.RequestTrade("roma", "moss", "templates/y", _t0.AddHours(1));
        withinCooldown.Should().Throw<HotStoveException>()
            .Which.Code.Should().Be(HotStoveErrorCode.TradeCooldownActive);

        // 49 hours after first approval (>48h cooldown) — should succeed.
        var afterCooldown = mgr.RequestTrade("roma", "moss", "templates/y", _t0.AddHours(49));
        afterCooldown.Should().NotBeNull();
    }

    [Fact]
    public void Cooldown_applies_to_both_traded_personas_independently()
    {
        var mgr = Build(out _);
        var first = mgr.RequestTrade("roma", "levene", "templates/x", _t0);
        mgr.ApproveTrade(first.TradeId, "jim", _t0.AddMinutes(1));

        // Levene's cooldown should block a different counterparty too.
        Action levBlocked = () => mgr.RequestTrade("levene", "moss", "y", _t0.AddHours(2));
        levBlocked.Should().Throw<HotStoveException>()
            .Which.Code.Should().Be(HotStoveErrorCode.TradeCooldownActive);
    }

    [Fact]
    public void ApproveTrade_unknown_id_throws()
    {
        var mgr = Build(out _);
        Action act = () => mgr.ApproveTrade("ghost", "jim", _t0);
        act.Should().Throw<HotStoveException>().Which.Code.Should().Be(HotStoveErrorCode.UnknownTrade);
    }

    [Fact]
    public void ApproveTrade_twice_throws_TradeAlreadyApplied()
    {
        var mgr = Build(out _);
        var trade = mgr.RequestTrade("roma", "levene", "x", _t0);
        mgr.ApproveTrade(trade.TradeId, "jim", _t0.AddMinutes(1));

        Action again = () => mgr.ApproveTrade(trade.TradeId, "jim", _t0.AddMinutes(5));
        again.Should().Throw<HotStoveException>().Which.Code.Should().Be(HotStoveErrorCode.TradeAlreadyApplied);
    }

    [Fact]
    public async Task EvaluateDraftForPromotion_positive_delta_promotes_and_emits_payload()
    {
        var mgr = Build(out _);
        var (draft, _) = await mgr.TrainAsync("roma", "c-1", TrainingScope.SingleTemplate, "v1");

        var payload = mgr.EvaluateDraftForPromotion(draft.DraftId, abDeltaScore: 1500.0, nowUtc: _t0.AddDays(1));
        payload.Should().NotBeNull();
        payload!.AbDeltaScore.Should().Be(1500.0);
        payload.Persona.Should().Be("roma");

        var stored = mgr.GetDraft(draft.DraftId)!;
        stored.IsPromoted.Should().BeTrue();
        stored.PromotedAtUtc.Should().Be(_t0.AddDays(1));
    }

    [Fact]
    public async Task EvaluateDraftForPromotion_below_floor_does_not_promote()
    {
        var options = new HotStoveOptions { DefaultAbPromotionFloor = 100.0 };
        var mgr = Build(out _, options: options);
        var (draft, _) = await mgr.TrainAsync("roma", "c-1", TrainingScope.SingleTemplate, "v1");

        var payload = mgr.EvaluateDraftForPromotion(draft.DraftId, abDeltaScore: 50.0, nowUtc: _t0.AddDays(1));
        payload.Should().BeNull();

        var stored = mgr.GetDraft(draft.DraftId)!;
        stored.IsPromoted.Should().BeFalse();
        stored.AbDeltaScore.Should().Be(50.0, "the delta is recorded even when not promoted");
    }

    [Fact]
    public async Task EvaluateDraftForPromotion_already_promoted_throws()
    {
        var mgr = Build(out _);
        var (draft, _) = await mgr.TrainAsync("roma", "c-1", TrainingScope.SingleTemplate, "v1");
        mgr.EvaluateDraftForPromotion(draft.DraftId, 200.0, _t0.AddDays(1));

        Action act = () => mgr.EvaluateDraftForPromotion(draft.DraftId, 300.0, _t0.AddDays(2));
        act.Should().Throw<HotStoveException>().Which.Code.Should().Be(HotStoveErrorCode.DraftAlreadyPromoted);
    }

    [Fact]
    public void EvaluateDraftForPromotion_unknown_draft_throws()
    {
        var mgr = Build(out _);
        Action act = () => mgr.EvaluateDraftForPromotion("ghost", 1.0, _t0);
        act.Should().Throw<HotStoveException>().Which.Code.Should().Be(HotStoveErrorCode.UnknownDraft);
    }

    [Fact]
    public void Constructor_rejects_null_training_agent()
    {
        Action act = () => _ = new InMemoryHotStoveManager(trainingAgent: null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
