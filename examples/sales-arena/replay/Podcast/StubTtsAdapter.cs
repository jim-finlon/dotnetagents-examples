namespace SalesArena.Replay.Podcast;

/// <summary>
/// Deterministic test TTS: emits silent PCM whose duration is one word ≈ the
/// configured pace. No audio quality; sufficient for section-assembly tests
/// and offline pipeline validation.
/// </summary>
public sealed class StubTtsAdapter : ITtsAdapter
{
    private readonly TimeSpan _wordPace;

    public StubTtsAdapter(TimeSpan? wordPace = null)
    {
        _wordPace = wordPace ?? TimeSpan.FromMilliseconds(280); // ~215 wpm
    }

    public Task<TtsClip> SynthesizeAsync(string text, string voiceId, int sampleRate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(voiceId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);

        var wordCount = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        var duration = TimeSpan.FromTicks(_wordPace.Ticks * Math.Max(1, wordCount));
        var samples = (int)(duration.TotalSeconds * sampleRate);
        // 16-bit mono silence: every sample is 2 bytes of zero.
        var bytes = new byte[samples * 2];
        return Task.FromResult(new TtsClip(bytes, duration));
    }
}
