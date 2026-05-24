using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetAgents.Voice.Transcription;
using DotNetAgents.Voice.Transcription.Models;

namespace SalesArena.Meeting;

public sealed record TaggedTurn(
    TranscriptTurn Turn,
    IReadOnlyList<string> Topics,
    bool LooksLikeDecision);

public interface ILiveMeetingCapture
{
    Task<MeetingTranscript> CaptureFromAudioFileAsync(
        ProspectId prospectId,
        string audioFilePath,
        DateTimeOffset startedAtUtc,
        CancellationToken ct = default);

    IReadOnlyList<TaggedTurn> LastTags { get; }
}

public interface IMeetingTopicTagger
{
    IReadOnlyList<string> TagTopics(string utterance);

    bool LooksLikeDecision(string utterance);
}

/// <summary>
/// Wraps <see cref="IVoiceTranscriptionService"/> so the Meeting Agent can ingest
/// audio captured during a live meeting and emit a <see cref="MeetingTranscript"/>
/// already enriched with topic tags + decision-marker flags. The tags are kept
/// on <see cref="LastTags"/> so callers (or the summarizer) can use them to weigh
/// turns without re-running the heuristic. The actual transcription is delegated
/// to the injected <see cref="IVoiceTranscriptionService"/> — Whisper local-runtime
/// when wired in production, a stub for unit/integration tests.
/// </summary>
public sealed class LiveMeetingCapture : ILiveMeetingCapture
{
    private readonly IVoiceTranscriptionService _transcription;
    private readonly IMeetingTopicTagger _tagger;
    private readonly TranscriptionOptions? _transcriptionOptions;
    private List<TaggedTurn> _lastTags = new();

    public LiveMeetingCapture(IVoiceTranscriptionService transcription)
        : this(transcription, new HeuristicMeetingTopicTagger(), null) { }

    public LiveMeetingCapture(IVoiceTranscriptionService transcription, IMeetingTopicTagger tagger)
        : this(transcription, tagger, null) { }

    public LiveMeetingCapture(
        IVoiceTranscriptionService transcription,
        IMeetingTopicTagger tagger,
        TranscriptionOptions? transcriptionOptions)
    {
        _transcription = transcription ?? throw new ArgumentNullException(nameof(transcription));
        _tagger = tagger ?? throw new ArgumentNullException(nameof(tagger));
        _transcriptionOptions = transcriptionOptions;
    }

    public IReadOnlyList<TaggedTurn> LastTags => _lastTags;

    public async Task<MeetingTranscript> CaptureFromAudioFileAsync(
        ProspectId prospectId,
        string audioFilePath,
        DateTimeOffset startedAtUtc,
        CancellationToken ct = default)
    {
        if (prospectId is null) throw new ArgumentNullException(nameof(prospectId));
        if (string.IsNullOrWhiteSpace(audioFilePath)) throw new ArgumentException("Audio file path required", nameof(audioFilePath));
        if (!File.Exists(audioFilePath)) throw new FileNotFoundException("Demo audio fixture not found", audioFilePath);

        var result = await _transcription.TranscribeAsync(audioFilePath, _transcriptionOptions, ct).ConfigureAwait(false);

        var turns = ParseTurns(result, startedAtUtc).ToList();
        _lastTags = turns
            .Select(turn => new TaggedTurn(turn, _tagger.TagTopics(turn.Text), _tagger.LooksLikeDecision(turn.Text)))
            .ToList();

        return new MeetingTranscript(prospectId, startedAtUtc, turns);
    }

    /// <summary>
    /// Splits a <see cref="TranscriptionResult"/> into <see cref="TranscriptTurn"/>s.
    /// Two formats are supported: a multi-line transcript where each non-blank line
    /// starts with <c>SPEAKER:</c> (preferred), or a single block of text that becomes
    /// one turn from <c>Narrator</c>. Timestamps are evenly distributed across the
    /// reported audio duration.
    /// </summary>
    private static IEnumerable<TranscriptTurn> ParseTurns(TranscriptionResult result, DateTimeOffset startedAtUtc)
    {
        _ = startedAtUtc;
        var lines = (result.Text ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
            yield break;

        var perTurn = result.Duration > TimeSpan.Zero
            ? TimeSpan.FromTicks(result.Duration.Ticks / Math.Max(lines.Length, 1))
            : TimeSpan.Zero;

        var index = 0;
        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');
            string speaker;
            string text;
            if (colonIndex > 0 && colonIndex < 32)
            {
                speaker = line.Substring(0, colonIndex).Trim();
                text = line.Substring(colonIndex + 1).Trim();
            }
            else
            {
                speaker = "Narrator";
                text = line;
            }

            yield return new TranscriptTurn(
                Speaker: string.IsNullOrEmpty(speaker) ? "Narrator" : speaker,
                Text: text,
                At: TimeSpan.FromTicks(perTurn.Ticks * index));
            index++;
        }
    }
}

/// <summary>
/// Deterministic topic tagger. Pure substring matching over case-folded utterances —
/// good enough to demonstrate the Meeting Agent's topic-tagging contract without
/// pulling an LLM into the slice. The HeuristicSummaryExtractionStrategy already
/// owns the decision-marker keyword list; this tagger mirrors it so the two stay
/// aligned without coupling the projects.
/// </summary>
public sealed class HeuristicMeetingTopicTagger : IMeetingTopicTagger
{
    private static readonly Dictionary<string, string[]> TopicKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pricing"]      = new[] { "pricing", "price", "cost", "discount", "budget", "license fee" },
        ["integration"]  = new[] { "integrate", "integration", "api", "webhook", "embed", "sdk" },
        ["security"]     = new[] { "security", "compliance", "soc2", "soc 2", "encryption", "audit", "iso 27001" },
        ["timeline"]     = new[] { "timeline", "deadline", "quarter", "month", "week", "by friday", "by monday", "kickoff" },
        ["scope"]        = new[] { "scope", "out of scope", "in scope", "phase one", "phase two", "mvp" },
        ["decision"]     = new[] { "decision", "decide", "agreed", "approved", "we'll go with", "we will go with" },
    };

    private static readonly string[] DecisionMarkers =
    {
        "decided", "we'll go with", "we will go with", "agreed to", "approved", "let's lock", "we're going with"
    };

    public IReadOnlyList<string> TagTopics(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
            return Array.Empty<string>();

        var lower = utterance.ToLower(CultureInfo.InvariantCulture);
        var tags = new List<string>();
        foreach (var (topic, keywords) in TopicKeywords)
        {
            if (keywords.Any(keyword => lower.Contains(keyword)))
                tags.Add(topic);
        }
        return tags;
    }

    public bool LooksLikeDecision(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
            return false;
        var lower = utterance.ToLower(CultureInfo.InvariantCulture);
        return DecisionMarkers.Any(marker => lower.Contains(marker));
    }
}
