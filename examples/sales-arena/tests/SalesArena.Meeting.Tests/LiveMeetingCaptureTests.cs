using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DotNetAgents.Voice.Transcription;
using DotNetAgents.Voice.Transcription.Models;
using FluentAssertions;
using Xunit;

namespace SalesArena.Meeting.Tests;

public sealed class LiveMeetingCaptureTests
{
    private static readonly TranscriptionResult CannedResult = new()
    {
        Text =
            "Alex: Thanks for taking the call today.\n" +
            "Sam: Happy to. We've been looking at pricing options.\n" +
            "Alex: We agreed to go with the platinum tier for SOC2 audit support.\n" +
            "Sam: Great, I'll send the security questionnaire by Friday.\n" +
            "Alex: Let's reconvene next quarter to revisit the integration scope.",
        Language = "en",
        Duration = TimeSpan.FromSeconds(4),
        Confidence = 0.95,
        SourceFilePath = "test://meeting-demo.wav",
    };

    [Fact]
    public async Task CaptureFromAudioFile_uses_transcription_service_and_returns_turns_in_order()
    {
        var stub = new StubTranscriptionService(CannedResult);
        var capture = new LiveMeetingCapture(stub);
        var prospect = new ProspectId("acme-corp");
        var fixturePath = ResolveFixturePath();

        var transcript = await capture.CaptureFromAudioFileAsync(
            prospect, fixturePath, new DateTimeOffset(2026, 5, 18, 17, 0, 0, TimeSpan.Zero));

        transcript.ProspectId.Should().Be(prospect);
        transcript.Turns.Should().HaveCount(5);
        transcript.Turns[0].Speaker.Should().Be("Alex");
        transcript.Turns[2].Text.Should().Contain("platinum tier");
        stub.LastAudioPath.Should().Be(fixturePath);
    }

    [Fact]
    public async Task Topic_tags_are_populated_for_known_themes()
    {
        var stub = new StubTranscriptionService(CannedResult);
        var capture = new LiveMeetingCapture(stub);
        await capture.CaptureFromAudioFileAsync(
            new ProspectId("acme-corp"), ResolveFixturePath(), DateTimeOffset.UtcNow);

        var tagsByText = capture.LastTags.ToDictionary(t => t.Turn.Text, t => t.Topics);
        tagsByText.Should().ContainKey("Happy to. We've been looking at pricing options.")
            .WhoseValue.Should().Contain("pricing");
        tagsByText.Keys.Single(k => k.Contains("platinum tier")).Pipe(key =>
        {
            tagsByText[key].Should().Contain("security");
        });
        tagsByText.Keys.Single(k => k.Contains("reconvene")).Pipe(key =>
        {
            tagsByText[key].Should().Contain("scope");
        });
    }

    [Fact]
    public async Task Decision_marker_is_detected_when_speaker_says_we_agreed()
    {
        var stub = new StubTranscriptionService(CannedResult);
        var capture = new LiveMeetingCapture(stub);
        await capture.CaptureFromAudioFileAsync(
            new ProspectId("acme-corp"), ResolveFixturePath(), DateTimeOffset.UtcNow);

        var decisionTurns = capture.LastTags.Where(t => t.LooksLikeDecision).ToList();
        decisionTurns.Should().NotBeEmpty();
        decisionTurns.Should().Contain(t => t.Turn.Text.Contains("agreed to go with"));
    }

    [Fact]
    public async Task Pipeline_wires_to_post_meeting_summarizer_and_publishes_crm_event()
    {
        var stub = new StubTranscriptionService(CannedResult);
        var capture = new LiveMeetingCapture(stub);
        var crm = new RecordingCrmEventPublisher();
        var summarizer = new PostMeetingSummarizer(crm);

        var transcript = await capture.CaptureFromAudioFileAsync(
            new ProspectId("acme-corp"), ResolveFixturePath(), DateTimeOffset.UtcNow);
        var summary = await summarizer.SummarizeAsync(transcript);

        summary.Decisions.Should().NotBeEmpty("the canned transcript contains 'agreed to go with'");
        summary.NextSteps.Should().NotBeEmpty("the canned transcript contains 'reconvene'");
        crm.Events.Should().HaveCount(1);
        crm.Events[0].Source.Should().Be(nameof(PostMeetingSummarizer));
    }

    [Fact]
    public async Task Missing_audio_file_throws_FileNotFoundException()
    {
        var stub = new StubTranscriptionService(CannedResult);
        var capture = new LiveMeetingCapture(stub);

        var act = () => capture.CaptureFromAudioFileAsync(
            new ProspectId("acme-corp"),
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.wav"),
            DateTimeOffset.UtcNow);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void Demo_audio_fixture_exists_and_is_a_valid_wav()
    {
        var fixturePath = ResolveFixturePath();
        File.Exists(fixturePath).Should().BeTrue($"the SA-01-05 demo audio fixture must exist at {fixturePath}");

        using var stream = File.OpenRead(fixturePath);
        using var reader = new BinaryReader(stream);
        var riff = new string(reader.ReadChars(4));
        reader.ReadInt32(); // chunk size
        var wave = new string(reader.ReadChars(4));
        riff.Should().Be("RIFF");
        wave.Should().Be("WAVE");
        stream.Length.Should().BeGreaterThan(1024, "the fixture should hold real PCM samples, not just headers");
    }

    private static string ResolveFixturePath()
    {
        // The test runs from bin/Debug/net10.0/; the fixture is checked in beside the synth.
        var here = Path.GetDirectoryName(typeof(LiveMeetingCaptureTests).Assembly.Location)!;
        var fixturePath = Path.GetFullPath(Path.Combine(
            here, "..", "..", "..", "..", "..",
            "agents", "calendar", "test-audio", "meeting-demo.wav"));
        return fixturePath;
    }

    private sealed class StubTranscriptionService : IVoiceTranscriptionService
    {
        private readonly TranscriptionResult _canned;

        public StubTranscriptionService(TranscriptionResult canned) => _canned = canned;

        public string? LastAudioPath { get; private set; }

        public Task<TranscriptionResult> TranscribeAsync(string audioFilePath, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastAudioPath = audioFilePath;
            return Task.FromResult(_canned with { SourceFilePath = audioFilePath });
        }

        public Task<TranscriptionResult> TranscribeAsync(Stream audioStream, TranscriptionOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_canned);

        public IReadOnlyList<string> GetSupportedFormats() => new[] { ".wav" };
    }
}

internal static class PipeExtension
{
    // Tiny inline pipe helper so the table-driven topic assertions read top-to-bottom.
    public static void Pipe<T>(this T value, Action<T> action) => action(value);
}
