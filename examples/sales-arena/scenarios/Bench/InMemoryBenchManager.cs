namespace SalesArena.Bench;

/// <summary>
/// In-memory bench manager. Keeps the active + reserve rosters as ordered
/// lists; reserve order is FIFO by JoinedSlotAtUtc so the oldest unused
/// bencher evicts first when capacity is hit.
/// </summary>
public sealed class InMemoryBenchManager : IBenchManager
{
    private readonly BenchOptions _options;
    private readonly List<BenchRosterEntry> _active;
    private readonly List<BenchRosterEntry> _reserve;
    private readonly Lock _lock = new();

    public InMemoryBenchManager(BenchOptions? options = null)
    {
        _options = options ?? new BenchOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxReserveSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.MaxActiveSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_options.ConsecutiveBelowFloorBeforeRelegation);
        _active = new List<BenchRosterEntry>(_options.MaxActiveSize);
        _reserve = new List<BenchRosterEntry>(_options.MaxReserveSize);
    }

    public IReadOnlyList<BenchRosterEntry> Active
    {
        get { lock (_lock) return _active.ToArray(); }
    }

    public IReadOnlyList<BenchRosterEntry> Reserve
    {
        get { lock (_lock) return _reserve.ToArray(); }
    }

    public bool IsActive(string persona)
    {
        lock (_lock) return _active.Any(e => e.Persona == persona);
    }

    public bool IsReserve(string persona)
    {
        lock (_lock) return _reserve.Any(e => e.Persona == persona);
    }

    public BenchEvent? AddToReserve(string persona, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        lock (_lock)
        {
            if (_active.Any(e => e.Persona == persona) || _reserve.Any(e => e.Persona == persona))
            {
                throw new BenchException(BenchErrorCode.AlreadyOnRoster, $"persona '{persona}' is already on the roster");
            }
            BenchEvent? eviction = null;
            if (_reserve.Count >= _options.MaxReserveSize)
            {
                var oldest = _reserve.MinBy(e => e.JoinedSlotAtUtc)!;
                _reserve.Remove(oldest);
                eviction = new BenchEvent(
                    Kind: BenchEventKinds.BenchEvicted,
                    Persona: oldest.Persona,
                    RelatedPersona: persona,
                    Reason: "reserve_capacity_fifo_eviction",
                    OccurredAtUtc: now);
            }
            _reserve.Add(new BenchRosterEntry(persona, BenchSlot.Reserve, now, 0));
            return eviction;
        }
    }

    public BenchEvent Promote(string persona, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        lock (_lock)
        {
            var entry = _reserve.SingleOrDefault(e => e.Persona == persona);
            if (entry is null)
            {
                throw new BenchException(BenchErrorCode.NotOnReserve, $"persona '{persona}' is not on the reserve bench");
            }
            if (_active.Count >= _options.MaxActiveSize)
            {
                throw new BenchException(BenchErrorCode.ActiveFloorFull,
                    $"active floor is full ({_options.MaxActiveSize}); relegate someone before promoting");
            }
            _reserve.Remove(entry);
            _active.Add(new BenchRosterEntry(persona, BenchSlot.Active, now, 0));
            return new BenchEvent(BenchEventKinds.PersonaPromoted, persona, null, reason, now);
        }
    }

    public BenchEvent Relegate(string persona, string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        lock (_lock)
        {
            var entry = _active.SingleOrDefault(e => e.Persona == persona);
            if (entry is null)
            {
                throw new BenchException(BenchErrorCode.NotActive, $"persona '{persona}' is not on the active floor");
            }
            _active.Remove(entry);
            // FIFO eviction may fire if reserve is already at MaxReserveSize.
            // The eviction is reported via LastEvictionFromLastRelegate so the
            // caller can surface it (the manual-relegate UI is a single button
            // press, so the API stays single-event for the common case).
            LastEvictionFromLastRelegate = TryAddToReserveAt(persona, now, out _);
            return new BenchEvent(BenchEventKinds.PersonaRelegated, persona, null, reason, now);
        }
    }

    /// <summary>
    /// Set after each <see cref="Relegate"/> call to whatever eviction-side
    /// effect the relegation caused (null when reserve had capacity).
    /// </summary>
    public BenchEvent? LastEvictionFromLastRelegate { get; private set; }

    private BenchEvent? TryAddToReserveAt(string persona, DateTimeOffset now, out BenchRosterEntry added)
    {
        BenchEvent? eviction = null;
        if (_reserve.Count >= _options.MaxReserveSize)
        {
            var oldest = _reserve.MinBy(e => e.JoinedSlotAtUtc)!;
            _reserve.Remove(oldest);
            eviction = new BenchEvent(BenchEventKinds.BenchEvicted, oldest.Persona, persona, "relegation_displaced_oldest_bencher", now);
        }
        added = new BenchRosterEntry(persona, BenchSlot.Reserve, now, 0);
        _reserve.Add(added);
        return eviction;
    }

    public IReadOnlyList<BenchEvent> RecordContestEnd(
        IReadOnlyDictionary<string, double> ratingsAtContestEnd,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ratingsAtContestEnd);
        var emitted = new List<BenchEvent>();
        lock (_lock)
        {
            // First pass: update consecutive-below counters.
            for (var i = 0; i < _active.Count; i++)
            {
                var entry = _active[i];
                if (!ratingsAtContestEnd.TryGetValue(entry.Persona, out var rating))
                {
                    // Missing rating = unchanged; preserve counter.
                    continue;
                }
                var nextCounter = rating < _options.EloFloor ? entry.ConsecutiveContestsBelowThreshold + 1 : 0;
                _active[i] = entry with { ConsecutiveContestsBelowThreshold = nextCounter };
            }

            // Second pass: relegate any active persona that crossed the threshold.
            // Iterate a snapshot so we can mutate _active inside the loop.
            foreach (var entry in _active.ToArray())
            {
                if (entry.ConsecutiveContestsBelowThreshold < _options.ConsecutiveBelowFloorBeforeRelegation)
                {
                    continue;
                }
                _active.Remove(entry);
                var maybeEviction = TryAddToReserveAt(entry.Persona, now, out _);
                emitted.Add(new BenchEvent(
                    Kind: BenchEventKinds.PersonaRelegated,
                    Persona: entry.Persona,
                    RelatedPersona: null,
                    Reason: $"auto_relegation_below_elo_{_options.EloFloor:F0}_for_{entry.ConsecutiveContestsBelowThreshold}_contests",
                    OccurredAtUtc: now));
                if (maybeEviction is not null)
                {
                    emitted.Add(maybeEviction);
                }
            }
        }
        return emitted;
    }
}
