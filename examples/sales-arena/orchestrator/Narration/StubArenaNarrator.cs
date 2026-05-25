using System.Collections.Concurrent;

namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Text-only narrator used by tests, headless replay, and any host that
/// cannot run a TTS engine. Captures every spoken cue in <see cref="Spoken"/>
/// and respects mute. Production hosts swap this for a real TTS adapter.
/// </summary>
public sealed class StubArenaNarrator : IArenaNarrator
{
    private readonly ConcurrentQueue<ArenaCue> _spoken = new();
    private int _mutedFlag;

    public bool IsMuted => Volatile.Read(ref _mutedFlag) == 1;

    public IReadOnlyList<ArenaCue> Spoken => _spoken.ToArray();

    public void Mute() => Interlocked.Exchange(ref _mutedFlag, 1);
    public void Unmute() => Interlocked.Exchange(ref _mutedFlag, 0);

    public Task SpeakAsync(ArenaCue cue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cue);
        if (IsMuted)
        {
            return Task.CompletedTask;
        }
        _spoken.Enqueue(cue);
        return Task.CompletedTask;
    }
}
