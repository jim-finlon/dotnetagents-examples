using SalesArena.Replay;

namespace SalesArena.Replay.Podcast;

/// <summary>
/// Default 5-section single-host renderer. Pulls source data from a
/// <see cref="ReplayReport"/> + the report's highlights to assemble:
/// cold-open → leaderboard-recap → deal-of-the-contest → comeback-story →
/// sign-off. Music-bed gaps between sections are inserted as silent PCM
/// at <see cref="PodcastOptions.SectionBedDuration"/>.
/// </summary>
public sealed class PodcastRenderer : IPodcastRenderer
{
    private readonly ITtsAdapter _tts;
    private readonly IAudioEncoder _encoder;
    private readonly PodcastOptions _options;

    public PodcastRenderer(ITtsAdapter tts, IAudioEncoder? encoder = null, PodcastOptions? options = null)
    {
        _tts = tts ?? throw new ArgumentNullException(nameof(tts));
        _encoder = encoder ?? new NullAudioEncoder();
        _options = options ?? new PodcastOptions();
    }

    public async Task<PodcastAudio> RenderAsync(ReplayReport report, VoicePack voicePack, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(voicePack);

        var script = BuildScript(report, voicePack);
        var pcm = new List<byte>(capacity: 1024 * 1024);
        var markers = new List<PodcastSectionMarker>(script.Count);
        var cursor = TimeSpan.Zero;

        for (var i = 0; i < script.Count; i++)
        {
            var section = script[i];
            var sectionStart = cursor;
            foreach (var line in section.Lines)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var clip = await _tts.SynthesizeAsync(line.Text, line.VoiceId, _options.SampleRate, cancellationToken).ConfigureAwait(false);
                pcm.AddRange(clip.PcmBytes);
                cursor += clip.Duration;
            }
            markers.Add(new PodcastSectionMarker(section.Kind, sectionStart, cursor - sectionStart));

            // Insert the music-bed gap as silence between sections (not after the last).
            if (i < script.Count - 1 && _options.SectionBedDuration > TimeSpan.Zero)
            {
                var silenceSamples = (int)(_options.SectionBedDuration.TotalSeconds * _options.SampleRate);
                pcm.AddRange(new byte[silenceSamples * (_options.BitsPerSample / 8) * _options.Channels]);
                cursor += _options.SectionBedDuration;
            }
        }

        var pcmArray = pcm.ToArray();
        var wav = WavWriter.Write(pcmArray, _options);

        var mp3 = await _encoder.EncodeAsync(pcmArray, _options, cancellationToken).ConfigureAwait(false);
        if (mp3 is not null && mp3.LongLength > _options.MaxMp3Bytes)
        {
            // Honest contract: refuse to ship an mp3 over the cap rather than
            // silently truncate audio. Caller can re-render with a stricter
            // bitrate option.
            throw new InvalidOperationException(
                $"encoded mp3 size {mp3.Length} exceeds cap {_options.MaxMp3Bytes}; reduce bitrate or shorten the report");
        }

        return new PodcastAudio(report.ContestId, wav, mp3, cursor, markers);
    }

    /// <summary>Build the unsynthesized 5-section script for a replay report.</summary>
    public static IReadOnlyList<PodcastScriptSection> BuildScript(ReplayReport report, VoicePack voicePack)
    {
        var sections = new List<PodcastScriptSection>(5);
        sections.Add(BuildColdOpen(report, voicePack));
        sections.Add(BuildLeaderboardRecap(report, voicePack));
        sections.Add(BuildDealOfTheContest(report, voicePack));
        sections.Add(BuildComebackStory(report, voicePack));
        sections.Add(BuildSignOff(report, voicePack));
        return sections;
    }

    private static PodcastScriptSection BuildColdOpen(ReplayReport report, VoicePack voicePack) =>
        new(PodcastSectionKind.ColdOpen, "Cold Open", new[]
        {
            new PodcastLine($"Contest {report.ContestId}. The floor is closed. The board is set.", voicePack.HostVoiceId, null),
            new PodcastLine("Five minutes from the bell back. Closers, replays, the deal that almost slipped, and the comeback that did not.", voicePack.HostVoiceId, null),
        });

    private static PodcastScriptSection BuildLeaderboardRecap(ReplayReport report, VoicePack voicePack)
    {
        var lines = new List<PodcastLine>
        {
            new("The final board.", voicePack.HostVoiceId, null),
        };
        var leaderboard = report.Sections.FirstOrDefault(s => s.Kind == ReplaySectionKind.Leaderboard);
        if (leaderboard is not null)
        {
            var summary = SummarizeMarkdown(leaderboard.Markdown);
            lines.Add(new PodcastLine(summary, voicePack.HostVoiceId, null));
        }
        else
        {
            lines.Add(new PodcastLine("No leaderboard recorded for this contest.", voicePack.HostVoiceId, null));
        }
        return new PodcastScriptSection(PodcastSectionKind.LeaderboardRecap, "Leaderboard Recap", lines);
    }

    private static PodcastScriptSection BuildDealOfTheContest(ReplayReport report, VoicePack voicePack)
    {
        var lines = new List<PodcastLine>
        {
            new("Deal of the contest.", voicePack.HostVoiceId, null),
        };
        var top = report.Highlights
            .Where(h => h.ValueUsd is > 0)
            .OrderByDescending(h => h.ValueUsd)
            .FirstOrDefault();
        if (top is not null)
        {
            var voice = voicePack.ResolveForPersona(top.Persona);
            lines.Add(new PodcastLine(top.Headline, voice, top.Persona));
            if (!string.IsNullOrEmpty(top.Persona))
            {
                lines.Add(new PodcastLine($"That was {top.Persona}.", voicePack.HostVoiceId, null));
            }
        }
        else
        {
            lines.Add(new PodcastLine("No closes worth featuring this contest.", voicePack.HostVoiceId, null));
        }
        return new PodcastScriptSection(PodcastSectionKind.DealOfTheContest, "Deal of the Contest", lines);
    }

    private static PodcastScriptSection BuildComebackStory(ReplayReport report, VoicePack voicePack)
    {
        var lines = new List<PodcastLine>
        {
            new("Comeback story.", voicePack.HostVoiceId, null),
        };
        var comeback = report.Sections.FirstOrDefault(s => s.Kind == ReplaySectionKind.BestComeback);
        if (comeback is not null)
        {
            lines.Add(new PodcastLine(SummarizeMarkdown(comeback.Markdown), voicePack.HostVoiceId, null));
        }
        else
        {
            lines.Add(new PodcastLine("No persona climbed enough to make the cut.", voicePack.HostVoiceId, null));
        }
        return new PodcastScriptSection(PodcastSectionKind.ComebackStory, "Comeback Story", lines);
    }

    private static PodcastScriptSection BuildSignOff(ReplayReport report, VoicePack voicePack) =>
        new(PodcastSectionKind.SignOff, "Sign-off", new[]
        {
            new PodcastLine("That is the recap. Closers, until the next contest, keep your phones on and your bells loaded.", voicePack.HostVoiceId, null),
            new PodcastLine($"Replay for contest {report.ContestId}, signed off.", voicePack.HostVoiceId, null),
        });

    /// <summary>
    /// Take the first ~3 non-trivial lines of a section's markdown for
    /// narration. Strips headers + table delimiters + blank lines.
    /// </summary>
    /// <summary>Trim markdown to a short narration-friendly summary.</summary>
    public static string SummarizeMarkdown(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "(no detail recorded)";
        var kept = markdown.Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#') && !l.StartsWith("---"))
            .Where(l => !(l.StartsWith('|') && l.Contains("---")))
            .ToArray();

        if (kept.Length == 0) return "(no detail recorded)";
        return string.Join(". ", kept.Take(3));
    }
}
