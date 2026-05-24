namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Sliding-window rate limiter for narrator output. Defaults to 5 events per
/// hour (story SA-02-06: bell spam mitigation in long contests). Operators
/// override per contest. ContestOpened is never rate-limited.
/// </summary>
public sealed class NarrationRateLimiter
{
    private readonly int _maxEvents;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly Queue<DateTimeOffset> _timestamps = new();
    private readonly Lock _lock = new();

    public NarrationRateLimiter(int maxEvents = 5, TimeSpan? window = null, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxEvents);
        _maxEvents = maxEvents;
        _window = window ?? TimeSpan.FromHours(1);
        if (_window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "window must be positive");
        }
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int MaxEvents => _maxEvents;
    public TimeSpan Window => _window;

    /// <summary>
    /// Returns true if a cue at the current time can proceed; false if the
    /// window is full. ContestOpened bypass is the caller's responsibility —
    /// the limiter is cue-agnostic.
    /// </summary>
    public bool TryAcquire()
    {
        var now = _timeProvider.GetUtcNow();
        lock (_lock)
        {
            EvictExpired(now);
            if (_timestamps.Count >= _maxEvents)
            {
                return false;
            }
            _timestamps.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Test/observer-only count of live timestamps in the window after eviction.
    /// </summary>
    public int CurrentCount()
    {
        var now = _timeProvider.GetUtcNow();
        lock (_lock)
        {
            EvictExpired(now);
            return _timestamps.Count;
        }
    }

    private void EvictExpired(DateTimeOffset now)
    {
        var cutoff = now - _window;
        while (_timestamps.Count > 0 && _timestamps.Peek() <= cutoff)
        {
            _timestamps.Dequeue();
        }
    }
}
