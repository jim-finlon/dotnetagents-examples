using FluentAssertions;
using SalesArena.Orchestrator.Coach;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Coach;

public sealed class PromptOverlayApplierTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static PromptOverlay ActiveOverlay(string speech = "halftime!", int remaining = 3) =>
        new(
            Persona: "roma",
            OperatorId: "jim",
            SanitizedSpeech: speech,
            InitialTouches: remaining,
            RemainingTouches: remaining,
            AppliedAtUtc: _t0,
            ExpiredAtUtc: null);

    [Fact]
    public void Compose_returns_base_prompt_unchanged_when_overlay_is_null()
    {
        PromptOverlayApplier.Compose("You are Roma.", null).Should().Be("You are Roma.");
    }

    [Fact]
    public void Compose_appends_addendum_when_overlay_is_active()
    {
        var combined = PromptOverlayApplier.Compose("You are Roma.", ActiveOverlay("Close the next three."));
        combined.Should().StartWith("You are Roma.");
        combined.Should().Contain(PromptOverlayApplier.AddendumHeader.TrimStart('\n'));
        combined.Should().Contain("Close the next three.");
        combined.Should().Contain("overlay expires");
    }

    [Fact]
    public void Compose_skips_addendum_when_overlay_has_expired()
    {
        var expired = ActiveOverlay() with { ExpiredAtUtc = _t0.AddMinutes(5), RemainingTouches = 0 };
        expired.IsActive.Should().BeFalse();
        PromptOverlayApplier.Compose("You are Roma.", expired).Should().Be("You are Roma.");
    }

    [Fact]
    public void Compose_skips_addendum_when_counter_reaches_zero()
    {
        var counterZero = ActiveOverlay(remaining: 0);
        counterZero.IsActive.Should().BeFalse();
        PromptOverlayApplier.Compose("You are Roma.", counterZero).Should().Be("You are Roma.");
    }

    [Fact]
    public void Compose_null_base_prompt_throws()
    {
        Action act = () => PromptOverlayApplier.Compose(null!, null);
        act.Should().Throw<ArgumentNullException>();
    }
}
