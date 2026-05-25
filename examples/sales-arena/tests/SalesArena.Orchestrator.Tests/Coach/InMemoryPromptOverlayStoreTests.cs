using FluentAssertions;
using SalesArena.Orchestrator.Coach;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Coach;

public sealed class InMemoryPromptOverlayStoreTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Inject_sets_an_active_overlay_for_persona()
    {
        var store = new InMemoryPromptOverlayStore();
        var (overlay, payload) = store.Inject(
            persona: "roma",
            operatorId: "jim",
            speech: "Close the next 3 like your rent is due.",
            expiresAfterTouches: 3,
            appliedAtUtc: _t0);

        overlay.Persona.Should().Be("roma");
        overlay.OperatorId.Should().Be("jim");
        overlay.SanitizedSpeech.Should().Be("Close the next 3 like your rent is due.");
        overlay.InitialTouches.Should().Be(3);
        overlay.RemainingTouches.Should().Be(3);
        overlay.IsActive.Should().BeTrue();
        overlay.AppliedAtUtc.Should().Be(_t0);

        payload.Persona.Should().Be("roma");
        payload.ExpiresAfterTouches.Should().Be(3);
        payload.SanitizedSpeech.Should().Be(overlay.SanitizedSpeech);

        store.GetActive("roma").Should().Be(overlay);
    }

    [Fact]
    public void ConsumeTouch_decrements_counter_until_expiry()
    {
        var store = new InMemoryPromptOverlayStore();
        store.Inject("levene", "jim", "Three more before lunch.", 3, _t0);

        store.ConsumeTouch("levene", _t0.AddSeconds(10)).Should().BeNull();
        store.GetActive("levene")!.RemainingTouches.Should().Be(2);

        store.ConsumeTouch("levene", _t0.AddSeconds(20)).Should().BeNull();
        store.GetActive("levene")!.RemainingTouches.Should().Be(1);

        var expired = store.ConsumeTouch("levene", _t0.AddSeconds(30));
        expired.Should().NotBeNull();
        expired!.TouchesConsumed.Should().Be(3);
        expired.Persona.Should().Be("levene");
        expired.ExpiredAtUtc.Should().Be(_t0.AddSeconds(30));

        store.GetActive("levene").Should().BeNull("overlay has expired and is no longer active");
    }

    [Fact]
    public void ConsumeTouch_with_no_active_overlay_is_noop()
    {
        var store = new InMemoryPromptOverlayStore();
        store.ConsumeTouch("ghost", _t0).Should().BeNull();
    }

    [Fact]
    public void Inject_replaces_an_existing_active_overlay()
    {
        var store = new InMemoryPromptOverlayStore();
        store.Inject("roma", "jim", "First speech.", 5, _t0);
        store.Inject("roma", "jim", "Forget the first; do this instead.", 2, _t0.AddMinutes(5));

        var active = store.GetActive("roma");
        active.Should().NotBeNull();
        active!.SanitizedSpeech.Should().Be("Forget the first; do this instead.");
        active.InitialTouches.Should().Be(2);
        active.RemainingTouches.Should().Be(2);
        active.AppliedAtUtc.Should().Be(_t0.AddMinutes(5));
    }

    [Fact]
    public void Inject_uses_default_expires_when_caller_omits()
    {
        var store = new InMemoryPromptOverlayStore(new CoachOptions { DefaultExpiresAfterTouches = 7 });
        var (overlay, _) = store.Inject("roma", "jim", "Default expiry test.");
        overlay.InitialTouches.Should().Be(7);
        overlay.RemainingTouches.Should().Be(7);
    }

    [Fact]
    public void Inject_rejects_zero_or_negative_expires()
    {
        var store = new InMemoryPromptOverlayStore();
        Action zero = () => store.Inject("roma", "jim", "x", expiresAfterTouches: 0);
        Action neg = () => store.Inject("roma", "jim", "x", expiresAfterTouches: -3);
        zero.Should().Throw<CoachException>().Which.Code.Should().Be(CoachErrorCode.ExpiresAfterMustBePositive);
        neg.Should().Throw<CoachException>().Which.Code.Should().Be(CoachErrorCode.ExpiresAfterMustBePositive);
    }

    [Fact]
    public void Inject_rejects_empty_operator()
    {
        var store = new InMemoryPromptOverlayStore();
        Action act = () => store.Inject("roma", "", "x", 3);
        act.Should().Throw<CoachException>().Which.Code.Should().Be(CoachErrorCode.OperatorRequired);
    }

    [Fact]
    public void GetActive_returns_null_when_no_overlay_present()
    {
        var store = new InMemoryPromptOverlayStore();
        store.GetActive("roma").Should().BeNull();
    }
}
