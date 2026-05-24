namespace SalesArena.Communications.Outbound;

/// <summary>
/// Hard cap of outbound sends per persona per UTC calendar day (SA-01-07: 200/day).
/// </summary>
public sealed class SendRateLimiter
{
    public const int DefaultDailyCap = 200;

    private readonly int _dailyCap;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, PersonaDayBucket> _buckets = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    public SendRateLimiter(int dailyCap = DefaultDailyCap, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dailyCap);
        _dailyCap = dailyCap;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public int DailyCap => _dailyCap;

    public bool TryAcquire(string personaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var day = _timeProvider.GetUtcNow().UtcDateTime.Date;
        lock (_lock)
        {
            var bucket = GetBucket(personaId, day);
            if (bucket.Count >= _dailyCap)
            {
                return false;
            }

            bucket.Count++;
            return true;
        }
    }

    public int GetSendCountToday(string personaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var day = _timeProvider.GetUtcNow().UtcDateTime.Date;
        lock (_lock)
        {
            return GetBucket(personaId, day).Count;
        }
    }

    private PersonaDayBucket GetBucket(string personaId, DateTime day)
    {
        if (!_buckets.TryGetValue(personaId, out var bucket) || bucket.Day != day)
        {
            bucket = new PersonaDayBucket(day, 0);
            _buckets[personaId] = bucket;
        }

        return bucket;
    }

    private sealed class PersonaDayBucket(DateTime day, int count)
    {
        public DateTime Day { get; } = day;
        public int Count { get; set; } = count;
    }
}
