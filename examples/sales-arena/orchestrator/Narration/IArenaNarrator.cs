namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// The narrator surface. Implementations may speak via TTS (Piper / Voice
/// Dialog / cloud) or stay text-only for tests, replay, and headless modes.
/// </summary>
public interface IArenaNarrator
{
    bool IsMuted { get; }
    void Mute();
    void Unmute();

    /// <summary>
    /// Speak a single resolved cue. Implementations are responsible for
    /// queueing to prevent overlapping audio (SA-02-06 perf note).
    /// </summary>
    Task SpeakAsync(ArenaCue cue, CancellationToken cancellationToken = default);
}
