using FluentAssertions;
using SalesArena.Communications.Outbound;
using Xunit;

namespace SalesArena.Communications.Outbound.Tests;

public sealed class SendRateLimiterTests
{
    [Fact]
    public void Refuses_after_daily_cap()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 18, 8, 0, 0, TimeSpan.Zero));
        var limiter = new SendRateLimiter(dailyCap: 3, timeProvider: time);

        limiter.TryAcquire("moss").Should().BeTrue();
        limiter.TryAcquire("moss").Should().BeTrue();
        limiter.TryAcquire("moss").Should().BeTrue();
        limiter.TryAcquire("moss").Should().BeFalse();

        time.Advance(TimeSpan.FromDays(1));
        limiter.TryAcquire("moss").Should().BeTrue();
    }

    [Fact]
    public async Task Send_service_returns_rate_limited_when_cap_hit()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 18, 8, 0, 0, TimeSpan.Zero));
        var tracker = new AbPromotionTracker();
        var loader = new SalesArena.OutreachTemplates.OutreachTemplateLoader(OutboundTestHost.PersonasRoot);
        var drafter = new OutboundDrafter(loader, tracker);
        var coordinator = new DotNetAgents.PreviewConfirm.PreviewConfirmCoordinator(
            new DotNetAgents.PreviewConfirm.InMemoryPreviewConfirmSessionStore(),
            TimeSpan.FromMinutes(30));
        var previewGate = new PreviewConfirmGate(coordinator, operatorPreviewDisabled: true);
        var rateLimiter = new SendRateLimiter(dailyCap: 2, timeProvider: time);
        var service = new OutboundSendService(drafter, tracker, previewGate, rateLimiter);
        var prospect = OutboundTestHost.SampleProspect;

        (await service.TrySendAsync(prospect, "williamson", "sms", OutboundIntent.Introduce))
            .Status.Should().Be(OutboundSendStatus.Sent);
        (await service.TrySendAsync(prospect, "williamson", "sms", OutboundIntent.Introduce))
            .Status.Should().Be(OutboundSendStatus.Sent);

        (await service.TrySendAsync(prospect, "williamson", "sms", OutboundIntent.Introduce))
            .Status.Should().Be(OutboundSendStatus.RateLimited);
    }
}
