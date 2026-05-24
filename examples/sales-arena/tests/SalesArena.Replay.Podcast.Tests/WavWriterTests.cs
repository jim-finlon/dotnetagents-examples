using FluentAssertions;
using SalesArena.Replay.Podcast;
using Xunit;

namespace SalesArena.Replay.Podcast.Tests;

public sealed class WavWriterTests
{
    [Fact]
    public void Write_produces_RIFF_WAVE_header()
    {
        var options = new PodcastOptions();
        var pcm = new byte[options.SampleRate * 2]; // 1 second of 16-bit mono silence
        var wav = WavWriter.Write(pcm, options);

        WavWriter.IsValidWav(wav).Should().BeTrue();
        wav.Length.Should().Be(pcm.Length + 44);
    }

    [Fact]
    public void Write_zero_length_pcm_still_produces_valid_header()
    {
        var wav = WavWriter.Write(Array.Empty<byte>(), new PodcastOptions());
        WavWriter.IsValidWav(wav).Should().BeTrue();
        wav.Length.Should().Be(44);
    }

    [Fact]
    public void IsValidWav_rejects_truncated_data()
    {
        WavWriter.IsValidWav(new byte[10]).Should().BeFalse();
    }

    [Fact]
    public void IsValidWav_rejects_non_RIFF_bytes()
    {
        var fake = new byte[64];
        Array.Fill<byte>(fake, 0x42);
        WavWriter.IsValidWav(fake).Should().BeFalse();
    }

    [Fact]
    public void Write_rejects_null_pcm()
    {
        Action act = () => WavWriter.Write(null!, new PodcastOptions());
        act.Should().Throw<ArgumentNullException>();
    }
}
