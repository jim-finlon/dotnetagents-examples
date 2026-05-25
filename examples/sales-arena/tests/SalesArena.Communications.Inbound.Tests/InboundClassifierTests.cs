using FluentAssertions;
using SalesArena.Communications.Inbound;
using Xunit;

namespace SalesArena.Communications.Inbound.Tests;

public sealed class InboundClassifierTests
{
    [Theory]
    [InlineData("Please unsubscribe immediately", InboundMessageIntent.Unsubscribe)]
    [InlineData("We have a pricing objection", InboundMessageIntent.Objection)]
    [InlineData("Can we schedule next week?", InboundMessageIntent.Scheduling)]
    [InlineData("Is SSO supported?", InboundMessageIntent.ColdQuestion)]
    [InlineData("This is spam viagra", InboundMessageIntent.Spam)]
    [InlineData("We are interested in a pilot", InboundMessageIntent.Interested)]
    public async Task Classifier_maps_intent_axis(string body, InboundMessageIntent expected)
    {
        var classifier = new InboundClassifier(new StubInboundClassificationModel());
        classifier.ProjectedPromptTemplate.Should().Contain("Sales Arena inbound message classifier");

        var result = await classifier.ClassifyAsync(Sample(body));
        result.Intent.Should().Be(expected);
    }

    [Theory]
    [InlineData("Thanks, this is great", InboundSentiment.Positive)]
    [InlineData("This is terrible service", InboundSentiment.Negative)]
    [InlineData("We are filing a hostile lawsuit", InboundSentiment.Hostile)]
    [InlineData("Following up on the deck", InboundSentiment.Neutral)]
    public async Task Classifier_maps_sentiment_axis(string body, InboundSentiment expected)
    {
        var classifier = new InboundClassifier(new StubInboundClassificationModel());
        var result = await classifier.ClassifyAsync(Sample(body));
        result.Sentiment.Should().Be(expected);
    }

    [Theory]
    [InlineData("Need this ASAP", InboundUrgency.Immediate)]
    [InlineData("Can we talk today?", InboundUrgency.Today)]
    [InlineData("Let's reconnect this week", InboundUrgency.ThisWeek)]
    [InlineData("No rush on the pilot", InboundUrgency.Nurture)]
    public async Task Classifier_maps_urgency_axis(string body, InboundUrgency expected)
    {
        var classifier = new InboundClassifier(new StubInboundClassificationModel());
        var result = await classifier.ClassifyAsync(Sample(body));
        result.Urgency.Should().Be(expected);
    }

    private static InboundMessage Sample(string body) =>
        new("m1", "email", "ext-1", body, "Test User", "test@example.com", "Acme", DateTimeOffset.UtcNow);
}
