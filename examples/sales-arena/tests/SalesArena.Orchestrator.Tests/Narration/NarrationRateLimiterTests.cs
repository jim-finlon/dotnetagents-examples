using FluentAssertions;
using SalesArena.Orchestrator.Narration;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Narration;

public sealed class NarrationRateLimiterTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_allows_five_per_hour_then_blocks()
    {
        var time = new FakeTimeProvider(_t0);
        var limiter = new NarrationRateLimiter(maxEvents: 5, window: TimeSpan.FromHours(1), timeProvider: time);

        for (var i = 0; i < 5; i++)
        {
            limiter.TryAcquire().Should().BeTrue();
        }

        limiter.TryAcquire().Should().BeFalse();
        limiter.CurrentCount().Should().Be(5);
    }

    [Fact]
    public void Window_slides_releases_oldest_first()
    {
        var time = new FakeTimeProvider(_t0);
        var limiter = new NarrationRateLimiter(maxEvents: 2, window: TimeSpan.FromMinutes(10), timeProvider: time);

        limiter.TryAcquire().Should().BeTrue();           // t0
        time.Advance(TimeSpan.FromMinutes(3));
        limiter.TryAcquire().Should().BeTrue();           // t0+3
        limiter.TryAcquire().Should().BeFalse();          // full

        time.Advance(TimeSpan.FromMinutes(8));            // t0+11 — first should have aged out
        limiter.TryAcquire().Should().BeTrue();           // first slot reclaimed
        limiter.TryAcquire().Should().BeFalse();
    }

    [Fact]
    public void Constructor_rejects_zero_or_negative_max_events()
    {
        Action zero = () => _ = new NarrationRateLimiter(maxEvents: 0);
        zero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_rejects_non_positive_window()
    {
        Action negative = () => _ = new NarrationRateLimiter(maxEvents: 1, window: TimeSpan.Zero);
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }
}
