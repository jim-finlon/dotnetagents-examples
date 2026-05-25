namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// Sliding-window rate limiter for bell dispatch. Default 5 bells/minute keeps
/// real channels from being spammed during a burst close cycle.
///
/// <para>Thread-safe. Decisions are exact at the boundary (no token-bucket
/// fuzziness): the Nth bell in any rolling 60-second window is the last one
/// allowed when the cap is N.</para>
/// </summary>
public sealed class BellRateLimiter
{
    private readonly int _maxPerWindow;
    private readonly TimeSpan _window;
    private readonly TimeProvider _time;
    private readonly Queue<DateTimeOffset> _recent = new();
    private readonly object _lock = new();

    public BellRateLimiter(int maxPerWindow = 5, TimeSpan? window = null, TimeProvider? time = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxPerWindow);
        _maxPerWindow = maxPerWindow;
        _window = window ?? TimeSpan.FromMinutes(1);
        _time = time ?? TimeProvider.System;
    }

    /// <summary>True when the limiter is disabled (cap of 0 means "no limiting").</summary>
    public bool IsDisabled => _maxPerWindow == 0;

    /// <summary>
    /// Attempt to consume one slot in the window. Returns true when allowed,
    /// false when the cap is reached. Decision is final; the caller does not
    /// need to call back on failure.
    /// </summary>
    public bool TryAcquire()
    {
        if (IsDisabled) return true;

        lock (_lock)
        {
            var now = _time.GetUtcNow();
            var threshold = now - _window;

            // Drop expired entries.
            while (_recent.Count > 0 && _recent.Peek() <= threshold)
            {
                _recent.Dequeue();
            }

            if (_recent.Count >= _maxPerWindow) return false;
            _recent.Enqueue(now);
            return true;
        }
    }

    /// <summary>Count of in-window dispatches; exposed for tests and observability.</summary>
    public int CurrentCount
    {
        get
        {
            lock (_lock)
            {
                var threshold = _time.GetUtcNow() - _window;
                while (_recent.Count > 0 && _recent.Peek() <= threshold)
                {
                    _recent.Dequeue();
                }
                return _recent.Count;
            }
        }
    }
}
