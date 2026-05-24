namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Outcome of a single radio tick. Useful for operator UIs that want to
/// show "nothing scheduled" vs "muted" vs "rate-limited" vs "spoken".
/// </summary>
public enum AmbientCueOutcome
{
    Spoken,
    Muted,
    NoCueSelected,
    TooSoonAfterBell,
    TooSoonAfterPriorAmbient,
    NoTemplate,
    RateLimited,
}

/// <summary>
/// The ambient narrator. Between bells, picks one cue per tick from the
/// live state snapshot. Pure decision logic — the SA-02-06 narrator does
/// the actual speaking.
///
/// <para>Priority (high → low): LeadAged → PersonaMomentum → ContestProgress
/// → GenericFiller. Higher-priority cues drown out filler so the radio never
/// reads boilerplate while a 6-hour-cold lead is begging for a follow-up.</para>
/// </summary>
public sealed class SalesFloorRadio
{
    private readonly IArenaNarrator _narrator;
    private readonly IArenaCueScriptResolver _resolver;
    private readonly NarrationRateLimiter _rateLimiter;
    private readonly SalesFloorRadioOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Lock _lock = new();

    private bool _muted;
    private DateTimeOffset? _muteOverrideExpiresAt;
    private DateTimeOffset? _lastAmbientAt;
    private TimeSpan _lastProgressBucket = TimeSpan.MinValue;

    public SalesFloorRadio(
        IArenaNarrator narrator,
        IArenaCueScriptResolver resolver,
        NarrationRateLimiter? rateLimiter = null,
        SalesFloorRadioOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _narrator = narrator ?? throw new ArgumentNullException(nameof(narrator));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _options = options ?? new SalesFloorRadioOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _rateLimiter = rateLimiter ?? new NarrationRateLimiter(maxEvents: 6, window: TimeSpan.FromHours(1), timeProvider: _timeProvider);
        _muted = _options.StartMuted;
    }

    public bool IsMuted
    {
        get
        {
            lock (_lock)
            {
                if (_muteOverrideExpiresAt is { } expires && _timeProvider.GetUtcNow() < expires)
                {
                    return false;
                }
                return _muted;
            }
        }
    }

    public void Mute()
    {
        lock (_lock)
        {
            _muted = true;
            _muteOverrideExpiresAt = null;
        }
    }

    /// <summary>
    /// Operator override: turn the radio on for <paramref name="duration"/>.
    /// When the window expires the radio reverts to its prior muted state
    /// (the underlying <c>_muted</c> flag is left intact).
    /// </summary>
    public void UnmuteFor(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration.Ticks);
        lock (_lock)
        {
            _muteOverrideExpiresAt = _timeProvider.GetUtcNow() + duration;
        }
    }

    /// <summary>
    /// Permanently unmute (until <see cref="Mute"/>). The operator's
    /// "leave it on" toggle.
    /// </summary>
    public void Unmute()
    {
        lock (_lock)
        {
            _muted = false;
            _muteOverrideExpiresAt = null;
        }
    }

    /// <summary>
    /// Tick the radio with a fresh state snapshot. Returns the resolved cue
    /// (if any) and the outcome.
    /// </summary>
    public async Task<(AmbientCueOutcome Outcome, ArenaCue? Cue)> TickAsync(
        RadioStateSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (IsMuted)
        {
            return (AmbientCueOutcome.Muted, null);
        }

        if (snapshot.TimeSinceLastBell < _options.MinSilenceAfterBell)
        {
            return (AmbientCueOutcome.TooSoonAfterBell, null);
        }

        var now = _timeProvider.GetUtcNow();
        lock (_lock)
        {
            if (_lastAmbientAt is { } last && (now - last) < _options.MinSpacingBetweenAmbient)
            {
                return (AmbientCueOutcome.TooSoonAfterPriorAmbient, null);
            }
        }

        var (cueKind, tokens, persona, leadId) = SelectCue(snapshot);
        if (cueKind is null)
        {
            return (AmbientCueOutcome.NoCueSelected, null);
        }

        var line = _resolver.Resolve(cueKind, tokens);
        if (line is null)
        {
            return (AmbientCueOutcome.NoTemplate, null);
        }

        if (!_rateLimiter.TryAcquire())
        {
            return (AmbientCueOutcome.RateLimited, null);
        }

        var cue = new ArenaCue(
            ContestId: snapshot.ContestId,
            CueKind: cueKind,
            Line: line,
            Persona: persona,
            LeadId: leadId,
            TimestampUtc: now,
            Tokens: tokens);

        await _narrator.SpeakAsync(cue, cancellationToken).ConfigureAwait(false);
        lock (_lock)
        {
            _lastAmbientAt = now;
            if (string.Equals(cueKind, AmbientCueKinds.ContestProgress, StringComparison.OrdinalIgnoreCase))
            {
                _lastProgressBucket = TruncateToBucket(snapshot.ContestElapsed, _options.ContestProgressEvery);
            }
        }
        return (AmbientCueOutcome.Spoken, cue);
    }

    private (string? CueKind, IReadOnlyDictionary<string, string> Tokens, string? Persona, string? LeadId)
        SelectCue(RadioStateSnapshot snapshot)
    {
        // 1) LeadAged — oldest qualifying lead wins.
        if (snapshot.AgedLeads.Count > 0)
        {
            var oldest = snapshot.AgedLeads
                .Where(l => l.SilentFor >= _options.LeadAgedThreshold)
                .OrderByDescending(l => l.SilentFor)
                .FirstOrDefault();
            if (oldest is not null)
            {
                var hours = (int)Math.Round(oldest.SilentFor.TotalHours, MidpointRounding.AwayFromZero);
                var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["lead"] = oldest.LeadId,
                    ["hours"] = hours.ToString(System.Globalization.CultureInfo.InvariantCulture),
                };
                return (AmbientCueKinds.LeadAged, tokens, null, oldest.LeadId);
            }
        }

        // 2) PersonaMomentum — highest-streak persona over threshold.
        var topStreak = snapshot.PersonaTouchStreaks
            .Where(kvp => kvp.Value >= _options.PersonaMomentumTouchThreshold)
            .OrderByDescending(kvp => kvp.Value)
            .ThenBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Cast<KeyValuePair<string, int>?>()
            .FirstOrDefault();
        if (topStreak is not null)
        {
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["persona"] = topStreak.Value.Key,
                ["touches"] = topStreak.Value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            return (AmbientCueKinds.PersonaMomentum, tokens, topStreak.Value.Key, null);
        }

        // 3) ContestProgress — fires once per ContestProgressEvery bucket.
        var bucket = TruncateToBucket(snapshot.ContestElapsed, _options.ContestProgressEvery);
        bool eligibleProgress;
        lock (_lock)
        {
            eligibleProgress = bucket > TimeSpan.Zero && bucket > _lastProgressBucket;
        }
        if (eligibleProgress)
        {
            var totalMinutes = (int)Math.Round(snapshot.ContestElapsed.TotalMinutes, MidpointRounding.AwayFromZero);
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["elapsed_minutes"] = totalMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["front_runner"] = snapshot.FrontRunner ?? "no-one yet",
                ["closes"] = snapshot.FrontRunnerCloses.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["runner_up"] = snapshot.Runner_up ?? "the pack",
            };
            return (AmbientCueKinds.ContestProgress, tokens, snapshot.FrontRunner, null);
        }

        // 4) GenericFiller — only when nothing has happened for a long stretch.
        if (snapshot.TimeSinceLastBell >= _options.InactivityFillerThreshold)
        {
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["quiet_minutes"] = ((int)snapshot.TimeSinceLastBell.TotalMinutes)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture),
            };
            return (AmbientCueKinds.GenericFiller, tokens, null, null);
        }

        return (null, EmptyTokens, null, null);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyTokens =
        new Dictionary<string, string>(0, StringComparer.Ordinal);

    private static TimeSpan TruncateToBucket(TimeSpan elapsed, TimeSpan bucket)
    {
        if (bucket <= TimeSpan.Zero || elapsed < bucket)
        {
            return TimeSpan.Zero;
        }
        var ticks = (elapsed.Ticks / bucket.Ticks) * bucket.Ticks;
        return new TimeSpan(ticks);
    }
}
