using FluentAssertions;
using SalesArena.Communications.Inbound;
using Xunit;

namespace SalesArena.Communications.Inbound.Tests;

public sealed class InboundPipelineTests
{
    [Fact]
    public async Task Pipeline_ingests_all_channels_and_dedupes()
    {
        var pipeline = InboundTestHost.BuildPipeline();
        var first = await pipeline.ProcessAsync();
        first.Should().HaveCount(4);

        var second = await pipeline.ProcessAsync();
        second.Should().BeEmpty();
    }

    [Fact]
    public async Task Pipeline_classifies_email_fixture_as_scheduling_positive_today()
    {
        var pipeline = InboundTestHost.BuildPipeline();
        var routed = await pipeline.ProcessAsync();
        var email = routed.Single(r => r.Message.Channel == "email");

        email.Classification.Intent.Should().Be(InboundMessageIntent.Scheduling);
        email.Classification.Sentiment.Should().Be(InboundSentiment.Positive);
        email.Classification.Urgency.Should().Be(InboundUrgency.Today);
        email.Correlation.Status.Should().Be(CrmCorrelationStatus.Matched);
        email.Correlation.LeadId.Should().Be("L-1001");
    }
}
