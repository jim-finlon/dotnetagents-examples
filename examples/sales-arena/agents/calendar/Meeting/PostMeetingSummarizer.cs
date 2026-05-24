using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SalesArena.Meeting;

public interface IPostMeetingSummarizer
{
    Task<MeetingSummary> SummarizeAsync(MeetingTranscript transcript, CancellationToken ct = default);
}

public interface ISummaryExtractionStrategy
{
    MeetingSummary Extract(MeetingTranscript transcript);
}

/// <summary>
/// Deterministic, offline summarizer. Uses keyword + speaker-turn heuristics so the
/// slice runs without an LLM. SA-01-05b can plug in an <see cref="ISummaryExtractionStrategy"/>
/// that calls a real model.
/// </summary>
public sealed class PostMeetingSummarizer : IPostMeetingSummarizer
{
    private readonly ISummaryExtractionStrategy _strategy;
    private readonly ICrmEventPublisher _crm;

    public PostMeetingSummarizer(ICrmEventPublisher crm)
        : this(new HeuristicSummaryExtractionStrategy(), crm) { }

    public PostMeetingSummarizer(ISummaryExtractionStrategy strategy, ICrmEventPublisher crm)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _crm = crm ?? throw new ArgumentNullException(nameof(crm));
    }

    public async Task<MeetingSummary> SummarizeAsync(MeetingTranscript transcript, CancellationToken ct = default)
    {
        if (transcript is null) throw new ArgumentNullException(nameof(transcript));

        var summary = _strategy.Extract(transcript);

        if (summary.NextSteps.Count > 0 || summary.Decisions.Count > 0)
        {
            await _crm.PublishStageChangeAsync(new CrmStageChangeEvent(
                ProspectId: transcript.ProspectId,
                FromStage: "MeetingHeld",
                ToStage: summary.Decisions.Count > 0 ? "DecisionRecorded" : "FollowUpScheduled",
                AtUtc: transcript.StartedAtUtc,
                Source: nameof(PostMeetingSummarizer)), ct).ConfigureAwait(false);
        }

        return summary;
    }
}

public sealed class HeuristicSummaryExtractionStrategy : ISummaryExtractionStrategy
{
    private static readonly string[] DecisionMarkers = { "decided", "we'll go with", "we will go with", "agreed to", "approved" };
    private static readonly string[] ActionMarkers = { "i'll send", "i will send", "we'll send", "follow up", "schedule", "draft" };
    private static readonly string[] ObjectionMarkers = { "concern", "worried", "risk", "but we need", "expensive", "not sure" };
    private static readonly string[] NextStepMarkers = { "next step", "next meeting", "let's reconvene", "circle back" };
    private static readonly string[] PositiveTokens = { "great", "excellent", "love", "perfect", "exactly" };
    private static readonly string[] NegativeTokens = { "concern", "worried", "doubt", "risk", "expensive" };

    public MeetingSummary Extract(MeetingTranscript transcript)
    {
        var decisions = new List<Decision>();
        var actions = new List<ActionItem>();
        var objections = new List<Objection>();
        var nextSteps = new List<NextStep>();
        var sentiments = new List<SentimentShift>();

        string previousSentiment = "neutral";
        foreach (var turn in transcript.Turns)
        {
            var lower = turn.Text.ToLowerInvariant();

            if (DecisionMarkers.Any(m => lower.Contains(m)))
                decisions.Add(new Decision(turn.Text.Trim(), turn.Speaker));

            if (ActionMarkers.Any(m => lower.Contains(m)))
                actions.Add(new ActionItem(turn.Text.Trim(), turn.Speaker, Due: null));

            if (ObjectionMarkers.Any(m => lower.Contains(m)))
                objections.Add(new Objection(turn.Text.Trim(), turn.Speaker));

            if (NextStepMarkers.Any(m => lower.Contains(m)))
                nextSteps.Add(new NextStep(turn.Text.Trim(), Due: null));

            var sentiment = ClassifySentiment(lower);
            if (!string.Equals(sentiment, previousSentiment, StringComparison.Ordinal))
            {
                sentiments.Add(new SentimentShift(previousSentiment, sentiment, turn.At));
                previousSentiment = sentiment;
            }
        }

        return new MeetingSummary(
            ProspectId: transcript.ProspectId,
            Decisions: decisions,
            Actions: actions,
            Objections: objections,
            NextSteps: nextSteps,
            SentimentShifts: sentiments);
    }

    private static string ClassifySentiment(string lowered)
    {
        bool positive = PositiveTokens.Any(t => lowered.Contains(t));
        bool negative = NegativeTokens.Any(t => lowered.Contains(t));
        return (positive, negative) switch
        {
            (true, false) => "positive",
            (false, true) => "negative",
            (true, true)  => "mixed",
            _              => "neutral",
        };
    }
}
