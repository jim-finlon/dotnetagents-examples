namespace SalesArena.Replay.Podcast;

/// <summary>
/// Encodes 16-bit signed PCM into a transport format (mp3, ogg, flac, …).
/// The default <see cref="NullAudioEncoder"/> returns null so callers know
/// to ship WAV only when no real encoder is registered. Production hosts
/// plug a real codec (NAudio.Lame, FFmpeg, etc.).
/// </summary>
public interface IAudioEncoder
{
    string FormatName { get; }
    Task<byte[]?> EncodeAsync(byte[] pcmBytes, PodcastOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default encoder: returns null. Used by tests + public-core hosts that
/// don't ship a codec dependency. The wav path always works.
/// </summary>
public sealed class NullAudioEncoder : IAudioEncoder
{
    public string FormatName => "mp3";
    public Task<byte[]?> EncodeAsync(byte[] pcmBytes, PodcastOptions options, CancellationToken cancellationToken = default)
        => Task.FromResult<byte[]?>(null);
}
