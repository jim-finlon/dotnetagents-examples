using FluentAssertions;
using SalesArena.Replay;
using SalesArena.Replay.Podcast;
using Xunit;

namespace SalesArena.Replay.Podcast.Tests;

public sealed class PodcastRendererTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 14, 0, 0, TimeSpan.Zero);

    private static ReplayReport SampleReport() => new(
        ContestId: "demo-1",
        GeneratedAtUtc: _t0,
        Markdown: "# Replay",
        Sections: new[]
        {
            new ReplaySection(ReplaySectionKind.Leaderboard, "Leaderboard",
                """
                # Leaderboard

                | # | Persona | Revenue |
                | --- | --- | --- |
                | 1 | roma | $48,000 |
                | 2 | levene | $35,000 |
                """),
            new ReplaySection(ReplaySectionKind.BestComeback, "Best Comeback",
                "moss climbed from position 6 to position 3 on a strong second hour."),
        },
        Highlights: new[]
        {
            new ReplayHighlight(ReplaySectionKind.PersonaDealLog,
                Headline: "roma closed Yatzee for $48,000.",
                Persona: "roma",
                LeadId: "L-9",
                ValueUsd: 48000m,
                OccurredAtUtc: _t0),
            new ReplayHighlight(ReplaySectionKind.PersonaDealLog,
                Headline: "levene closed Sigma for $35,000.",
                Persona: "levene",
                LeadId: "L-7",
                ValueUsd: 35000m,
                OccurredAtUtc: _t0),
        });

    private static VoicePack SamplePack() => new()
    {
        HostVoiceId = "host-male-warm",
        PersonaVoices = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["roma"] = "voice-roma",
            ["levene"] = "voice-levene",
        },
    };

    [Fact]
    public async Task RenderAsync_produces_5_section_audio()
    {
        var renderer = new PodcastRenderer(new StubTtsAdapter());
        var audio = await renderer.RenderAsync(SampleReport(), SamplePack());

        audio.SectionMarkers.Should().HaveCount(5);
        audio.SectionMarkers.Select(m => m.Kind).Should().BeEquivalentTo(new[]
        {
            PodcastSectionKind.ColdOpen,
            PodcastSectionKind.LeaderboardRecap,
            PodcastSectionKind.DealOfTheContest,
            PodcastSectionKind.ComebackStory,
            PodcastSectionKind.SignOff,
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task RenderAsync_emits_valid_wav_header()
    {
        var renderer = new PodcastRenderer(new StubTtsAdapter());
        var audio = await renderer.RenderAsync(SampleReport(), SamplePack());

        WavWriter.IsValidWav(audio.WavBytes).Should().BeTrue();
        audio.WavBytes.Length.Should().BeGreaterThan(44, "wav header alone is 44 bytes; clip data must follow");
    }

    [Fact]
    public async Task RenderAsync_null_encoder_produces_null_mp3()
    {
        var renderer = new PodcastRenderer(new StubTtsAdapter(), new NullAudioEncoder());
        var audio = await renderer.RenderAsync(SampleReport(), SamplePack());
        audio.Mp3Bytes.Should().BeNull();
    }

    [Fact]
    public async Task RenderAsync_voice_pack_assigns_persona_voice_to_deal_of_contest_quote()
    {
        var captured = new CapturingTtsAdapter();
        var renderer = new PodcastRenderer(captured);
        await renderer.RenderAsync(SampleReport(), SamplePack());

        // Find the line whose text matches the persona-attributed headline; it
        // should have been synthesized with the roma voice id from the pack.
        captured.Calls.Should().Contain(c => c.Text.Contains("roma closed Yatzee") && c.VoiceId == "voice-roma");
    }

    [Fact]
    public async Task RenderAsync_unknown_persona_falls_back_to_fallback_voice()
    {
        var report = SampleReport() with
        {
            Highlights = new[]
            {
                new ReplayHighlight(ReplaySectionKind.PersonaDealLog,
                    "ghost closed something for $100.",
                    Persona: "ghost",
                    LeadId: "L-1",
                    ValueUsd: 100m,
                    OccurredAtUtc: _t0),
            },
        };
        var pack = SamplePack() with { FallbackVoiceId = "voice-fallback-x" };
        var captured = new CapturingTtsAdapter();
        var renderer = new PodcastRenderer(captured);
        await renderer.RenderAsync(report, pack);

        captured.Calls.Should().Contain(c => c.Text.Contains("ghost closed something") && c.VoiceId == "voice-fallback-x");
    }

    [Fact]
    public async Task RenderAsync_empty_highlights_falls_back_to_no_close_line()
    {
        var report = SampleReport() with { Highlights = Array.Empty<ReplayHighlight>() };
        var captured = new CapturingTtsAdapter();
        var renderer = new PodcastRenderer(captured);
        await renderer.RenderAsync(report, SamplePack());

        captured.Calls.Should().Contain(c => c.Text == "No closes worth featuring this contest.");
    }

    [Fact]
    public async Task RenderAsync_section_bed_inserts_silence_between_sections()
    {
        var options = new PodcastOptions { SectionBedDuration = TimeSpan.FromSeconds(1) };
        var renderer = new PodcastRenderer(new StubTtsAdapter(), null, options);
        var audio = await renderer.RenderAsync(SampleReport(), SamplePack());

        // 5 sections → 4 bed gaps of 1s each at minimum.
        audio.Duration.Should().BeGreaterThan(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public async Task RenderAsync_mp3_over_cap_is_rejected()
    {
        var renderer = new PodcastRenderer(
            new StubTtsAdapter(),
            new OversizedEncoder(),
            new PodcastOptions { MaxMp3Bytes = 1024 });

        Func<Task> act = () => renderer.RenderAsync(SampleReport(), SamplePack());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exceeds cap*");
    }

    [Fact]
    public async Task RenderAsync_uses_host_voice_for_narration_outside_persona_quotes()
    {
        var captured = new CapturingTtsAdapter();
        var renderer = new PodcastRenderer(captured);
        await renderer.RenderAsync(SampleReport(), SamplePack());

        var coldOpenCalls = captured.Calls.Where(c => c.Text.Contains("Contest demo-1")).ToArray();
        coldOpenCalls.Should().NotBeEmpty();
        coldOpenCalls.Should().OnlyContain(c => c.VoiceId == "host-male-warm");
    }

    [Fact]
    public void SummarizeMarkdown_strips_headers_table_delimiters_and_blanks()
    {
        var summary = PodcastRenderer.SummarizeMarkdown(
            """
            # Header

            | # | Persona | Revenue |
            | --- | --- | --- |
            | 1 | roma | $48,000 |
            | 2 | levene | $35,000 |
            """);
        summary.Should().NotContain("---");
        summary.Should().NotContain("# Header");
        summary.Should().Contain("roma");
    }

    [Fact]
    public void SummarizeMarkdown_empty_returns_placeholder()
    {
        PodcastRenderer.SummarizeMarkdown("").Should().Contain("no detail");
        PodcastRenderer.SummarizeMarkdown(null!).Should().Contain("no detail");
    }

    [Fact]
    public void BuildScript_produces_5_sections_in_order()
    {
        var script = PodcastRenderer.BuildScript(SampleReport(), SamplePack());
        script.Should().HaveCount(5);
        script.Select(s => s.Kind).Should().BeEquivalentTo(new[]
        {
            PodcastSectionKind.ColdOpen,
            PodcastSectionKind.LeaderboardRecap,
            PodcastSectionKind.DealOfTheContest,
            PodcastSectionKind.ComebackStory,
            PodcastSectionKind.SignOff,
        }, opts => opts.WithStrictOrdering());
    }

    private sealed class CapturingTtsAdapter : ITtsAdapter
    {
        public List<(string Text, string VoiceId)> Calls { get; } = new();

        public Task<TtsClip> SynthesizeAsync(string text, string voiceId, int sampleRate, CancellationToken cancellationToken = default)
        {
            Calls.Add((text, voiceId));
            // Trivial 1-sample silent clip so wav writer is happy.
            return Task.FromResult(new TtsClip(new byte[2], TimeSpan.FromMilliseconds(50)));
        }
    }

    private sealed class OversizedEncoder : IAudioEncoder
    {
        public string FormatName => "mp3";
        public Task<byte[]?> EncodeAsync(byte[] pcmBytes, PodcastOptions options, CancellationToken cancellationToken = default)
        {
            // 10 KB > 1 KB cap.
            return Task.FromResult<byte[]?>(new byte[10 * 1024]);
        }
    }
}
