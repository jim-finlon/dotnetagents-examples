namespace SalesArena.Replay.Podcast;

/// <summary>
/// Synthesizes one short clip to 16-bit signed PCM samples at the
/// configured sample rate. Implementations vary: local Piper, Voice.Dialog,
/// cloud TTS. Tests + offline mode use <see cref="StubTtsAdapter"/>.
/// </summary>
public interface ITtsAdapter
{
    /// <summary>
    /// Render <paramref name="text"/> in <paramref name="voiceId"/> at
    /// <paramref name="sampleRate"/> Hz. Return the raw PCM byte array
    /// (16-bit signed little-endian).
    /// </summary>
    Task<TtsClip> SynthesizeAsync(string text, string voiceId, int sampleRate, CancellationToken cancellationToken = default);
}

public sealed record TtsClip(byte[] PcmBytes, TimeSpan Duration);
