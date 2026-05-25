using System;
using FluentAssertions;
using Xunit;

namespace SalesArena.Meeting.Tests;

public class PostMeetingSummarizerTests
{
    private static MeetingTranscript Transcript(params (string speaker, string text, int seconds)[] turns)
    {
        var list = new System.Collections.Generic.List<TranscriptTurn>();
        foreach (var (speaker, text, seconds) in turns)
            list.Add(new TranscriptTurn(speaker, text, TimeSpan.FromSeconds(seconds)));
        return new MeetingTranscript(new ProspectId("greybridge"), DateTimeOffset.UtcNow, list);
    }

    [Fact]
    public async Task Summarize_ExtractsDecisions()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        var summary = await sut.SummarizeAsync(Transcript(
            ("Moss",   "We decided to go with the Pro tier.", 10),
            ("Buyer",  "Sounds great.",                       12)));

        summary.Decisions.Should().ContainSingle().Which.Text.Should().Contain("decided");
        summary.Decisions[0].Owner.Should().Be("Moss");
    }

    [Fact]
    public async Task Summarize_ExtractsActions()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        var summary = await sut.SummarizeAsync(Transcript(
            ("Moss", "I'll send the redlines tonight.", 5),
            ("Moss", "Then we'll schedule the legal review.", 7)));

        summary.Actions.Should().HaveCount(2);
        summary.Actions[0].Owner.Should().Be("Moss");
    }

    [Fact]
    public async Task Summarize_ExtractsObjections()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        var summary = await sut.SummarizeAsync(Transcript(
            ("Buyer", "Our concern is the implementation risk.", 30),
            ("Moss",  "Fair.", 32)));

        summary.Objections.Should().ContainSingle().Which.Speaker.Should().Be("Buyer");
    }

    [Fact]
    public async Task Summarize_ExtractsNextSteps()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        var summary = await sut.SummarizeAsync(Transcript(
            ("Moss", "Next step: I'll send the SOW.", 100),
            ("Buyer", "Let's reconvene Thursday.", 110)));

        summary.NextSteps.Should().HaveCount(2);
    }

    [Fact]
    public async Task Summarize_DetectsSentimentShift()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        var summary = await sut.SummarizeAsync(Transcript(
            ("Moss",  "I love the proposal.", 1),
            ("Buyer", "Our concern is the price.", 5)));

        summary.SentimentShifts.Should().NotBeEmpty();
        summary.SentimentShifts.Should().Contain(s => s.To == "positive");
        summary.SentimentShifts.Should().Contain(s => s.To == "negative");
    }

    [Fact]
    public async Task Summarize_PublishesCrmEvent_WhenDecisionPresent()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        await sut.SummarizeAsync(Transcript(
            ("Moss", "We agreed to ship by Q3.", 1)));

        crm.Events.Should().ContainSingle();
        crm.Events[0].ToStage.Should().Be("DecisionRecorded");
    }

    [Fact]
    public async Task Summarize_PublishesFollowUp_WhenOnlyNextStepPresent()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        await sut.SummarizeAsync(Transcript(
            ("Moss", "Next step: send the deck.", 1)));

        crm.Events.Should().ContainSingle();
        crm.Events[0].ToStage.Should().Be("FollowUpScheduled");
    }

    [Fact]
    public async Task Summarize_NoSignals_DoesNotPublish()
    {
        var crm = new RecordingCrmEventPublisher();
        var sut = new PostMeetingSummarizer(crm);

        await sut.SummarizeAsync(Transcript(
            ("Moss",  "Hi.",          1),
            ("Buyer", "Hello there.", 2)));

        crm.Events.Should().BeEmpty();
    }
}
