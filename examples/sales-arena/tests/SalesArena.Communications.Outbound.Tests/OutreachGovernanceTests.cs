using FluentAssertions;
using SalesArena.Communications.Outbound;
using Xunit;

namespace SalesArena.Communications.Outbound.Tests;

public sealed class OutreachGovernanceTests
{
    [Fact]
    public async Task Refuses_no_opt_out_prospect()
    {
        var service = OutboundTestHost.BuildSendService(out _, out _, out _, operatorPreviewDisabled: true);
        var prospect = OutboundTestHost.SampleProspect with { OutreachOptIn = false };

        var result = await service.TrySendAsync(prospect, "roma", "email", OutboundIntent.Introduce);
        result.Status.Should().Be(OutboundSendStatus.GovernanceRefused);
    }
}
