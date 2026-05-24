namespace SalesArena.Replay.Podcast;

/// <summary>
/// Tunables for the renderer. Defaults: 5MB Mp3 ceiling, 5-minute target
/// length, 22.05 kHz / 16-bit / mono PCM (radio-grade, small file size).
/// </summary>
public sealed record PodcastOptions
{
    public long MaxMp3Bytes { get; init; } = 5L * 1024 * 1024;
    public TimeSpan TargetDuration { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan SectionBedDuration { get; init; } = TimeSpan.FromMilliseconds(750);

    /// <summary>WAV sample rate. 22050 Hz is the radio standard; small file size + intelligible voice.</summary>
    public int SampleRate { get; init; } = 22050;

    /// <summary>16-bit signed PCM. Required for the WAV writer to ship valid RIFF.</summary>
    public int BitsPerSample { get; init; } = 16;

    /// <summary>Mono. Cheaper than stereo; voice is mono anyway.</summary>
    public int Channels { get; init; } = 1;
}
