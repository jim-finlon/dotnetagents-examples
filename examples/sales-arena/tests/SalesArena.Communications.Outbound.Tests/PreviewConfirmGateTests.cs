using FluentAssertions;
using SalesArena.Communications.Outbound;
using Xunit;

namespace SalesArena.Communications.Outbound.Tests;

public sealed class PreviewConfirmGateTests
{
    [Fact]
    public async Task First_sends_require_preview_until_threshold_confirmed()
    {
        var service = OutboundTestHost.BuildSendService(
            out _,
            out var gate,
            out _,
            operatorPreviewDisabled: false);
        var prospect = OutboundTestHost.SampleProspect;

        gate.RequiresPreview("roma").Should().BeTrue();
        var pending = await service.TrySendAsync(prospect, "roma", "sms", OutboundIntent.Introduce);
        pending.Status.Should().Be(OutboundSendStatus.PreviewRequired);
        pending.PreviewSessionId.Should().NotBeNull();
        pending.PreviewToken.Should().NotBeNullOrEmpty();

        var confirmed = await service.ConfirmPreviewAndSendAsync(
            pending.PreviewSessionId!.Value,
            pending.PreviewToken!);
        confirmed.Status.Should().Be(OutboundSendStatus.Sent);
        gate.GetConfirmedSendCount("roma").Should().Be(1);
    }

    [Fact]
    public async Task After_threshold_preview_not_required_when_operator_flag_set()
    {
        var service = OutboundTestHost.BuildSendService(
            out _,
            out _,
            out _,
            operatorPreviewDisabled: true);
        var result = await service.TrySendAsync(
            OutboundTestHost.SampleProspect,
            "levene",
            "chat",
            OutboundIntent.MeetingNudge);
        result.Status.Should().Be(OutboundSendStatus.Sent);
    }
}
