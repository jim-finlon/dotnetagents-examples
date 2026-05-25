using FluentAssertions;
using Xunit;

namespace SalesArena.AudioCues.Tests;

/// <summary>
/// SA-06-04 audio-cue acceptance: every cue exists, is a valid RIFF WAVE,
/// is ≤ 500 KB, and the folder totals ≤ 3 MB. Source provenance is
/// in samples/sales-arena/assets/audio/LICENSE.md.
/// </summary>
public sealed class AudioCueAssetsTests
{
    private const long PerFileCapBytes = 500 * 1024;
    private const long FolderCapBytes = 3 * 1024 * 1024;

    public static IEnumerable<object[]> RequiredFiles() => new[]
    {
        new object[] { "bell.wav" },
        new object[] { "drumroll.wav" },
        new object[] { "sad-trombone.wav" },
        new object[] { "cold-open.wav" },
    };

    [Theory]
    [MemberData(nameof(RequiredFiles))]
    public void Cue_file_exists(string name)
    {
        var path = Path.Combine(AudioDir(), name);
        File.Exists(path).Should().BeTrue($"SA-06-04 acceptance requires {name} under samples/sales-arena/assets/audio/");
    }

    [Theory]
    [MemberData(nameof(RequiredFiles))]
    public void Cue_file_is_valid_riff_wave(string name)
    {
        var path = Path.Combine(AudioDir(), name);
        var bytes = File.ReadAllBytes(path);
        bytes.Length.Should().BeGreaterThan(44, "RIFF header is 44 bytes; PCM data must follow");
        bytes[0..4].Should().Equal((byte)'R', (byte)'I', (byte)'F', (byte)'F');
        bytes[8..12].Should().Equal((byte)'W', (byte)'A', (byte)'V', (byte)'E');
        bytes[12..16].Should().Equal((byte)'f', (byte)'m', (byte)'t', (byte)' ');
    }

    [Theory]
    [MemberData(nameof(RequiredFiles))]
    public void Cue_file_is_within_per_file_cap(string name)
    {
        var path = Path.Combine(AudioDir(), name);
        var size = new FileInfo(path).Length;
        size.Should().BeLessOrEqualTo(PerFileCapBytes,
            $"SA-06-04 caps each cue at 500 KB; {name} is {size} bytes");
    }

    [Fact]
    public void Audio_folder_total_is_within_3_MB_cap()
    {
        var dir = AudioDir();
        long total = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*.wav"))
        {
            total += new FileInfo(f).Length;
        }
        total.Should().BeLessOrEqualTo(FolderCapBytes,
            $"SA-06-04 caps the audio/ folder at 3 MB; current total {total} bytes");
    }

    [Fact]
    public void License_file_has_no_pending_placeholder_rows()
    {
        var licensePath = Path.Combine(AudioDir(), "LICENSE.md");
        File.Exists(licensePath).Should().BeTrue();
        var content = File.ReadAllText(licensePath);
        content.Should().NotContain("(pending)",
            "every placeholder row must be replaced with the synthesized cue's provenance");
        content.Should().Contain("CC0", "the license dedication must be explicit");
    }

    [Fact]
    public void Synth_project_is_present_so_audio_can_be_regenerated()
    {
        var synthProj = Path.Combine(AudioDir(), "_synth", "AudioCueSynth", "AudioCueSynth.csproj");
        File.Exists(synthProj).Should().BeTrue(
            "the DNA-authored synth project is the provenance for every cue; LICENSE.md links to it");
    }

    private static string AudioDir()
    {
        // Test bin/Debug/net10.0 → up five → samples/sales-arena/assets/audio/
        var candidate = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "assets", "audio"));
        if (Directory.Exists(candidate)) return candidate;
        throw new DirectoryNotFoundException($"could not locate samples/sales-arena/assets/audio/ (looked at {candidate})");
    }
}
