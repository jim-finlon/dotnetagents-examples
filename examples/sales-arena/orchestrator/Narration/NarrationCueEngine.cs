using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Outcome of an engine handle attempt. Useful for tests and replay where
/// the caller wants to verify whether a cue was rate-limited or missing-template.
/// </summary>
public enum CueDispatchOutcome
{
    Spoken,
    RateLimited,
    NoTemplate,
    UnsupportedEvent,
    Muted,
}

/// <summary>
/// Translates ledger <see cref="ArenaEvent"/>s into narrator <see cref="ArenaCue"/>s.
/// Selects the cue kind, resolves a script line, rate-limits, and dispatches to
/// the narrator. Pure orchestration — no I/O beyond what the resolver does.
/// </summary>
public sealed class NarrationCueEngine
{
    private readonly IArenaNarrator _narrator;
    private readonly IArenaCueScriptResolver _resolver;
    private readonly NarrationRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public NarrationCueEngine(
        IArenaNarrator narrator,
        IArenaCueScriptResolver resolver,
        NarrationRateLimiter? rateLimiter = null,
        TimeProvider? timeProvider = null)
    {
        _narrator = narrator ?? throw new ArgumentNullException(nameof(narrator));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _rateLimiter = rateLimiter ?? new NarrationRateLimiter();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Handle a ledger event. Returns the cue + outcome. ContestOpened
    /// bypasses the rate limiter so the cold open always plays.
    /// </summary>
    public async Task<(CueDispatchOutcome Outcome, ArenaCue? Cue)> HandleAsync(
        ArenaEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        var (cueKind, tokens, persona, leadId) = MapEvent(evt);
        if (cueKind is null)
        {
            return (CueDispatchOutcome.UnsupportedEvent, null);
        }

        var line = _resolver.Resolve(cueKind, tokens);
        if (line is null)
        {
            return (CueDispatchOutcome.NoTemplate, null);
        }

        var cue = new ArenaCue(
            ContestId: evt.ContestId,
            CueKind: cueKind,
            Line: line,
            Persona: persona,
            LeadId: leadId,
            TimestampUtc: _timeProvider.GetUtcNow(),
            Tokens: tokens);

        if (_narrator.IsMuted)
        {
            return (CueDispatchOutcome.Muted, cue);
        }

        var bypassLimiter = string.Equals(cueKind, CueKinds.ContestOpened, StringComparison.OrdinalIgnoreCase);
        if (!bypassLimiter && !_rateLimiter.TryAcquire())
        {
            return (CueDispatchOutcome.RateLimited, cue);
        }

        await _narrator.SpeakAsync(cue, cancellationToken).ConfigureAwait(false);
        return (CueDispatchOutcome.Spoken, cue);
    }

    private static (string? CueKind, IReadOnlyDictionary<string, string> Tokens, string? Persona, string? LeadId)
        MapEvent(ArenaEvent evt)
    {
        var tokens = new Dictionary<string, string>(StringComparer.Ordinal);

        switch (evt.Kind)
        {
            case ArenaEventKinds.ContestPhaseChanged:
            {
                var payload = evt.GetPayload<ContestPhaseChangedPayload>();
                if (payload is null || !string.Equals(payload.Phase, "Started", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, EmptyTokens, null, null);
                }
                tokens["contest"] = evt.ContestId;
                return (CueKinds.ContestOpened, tokens, null, null);
            }
            case ArenaEventKinds.DealClosed:
            {
                var payload = evt.GetPayload<DealClosedPayload>();
                if (payload is null || !string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
                {
                    return (null, EmptyTokens, null, null);
                }
                tokens["persona"] = payload.Persona;
                tokens["lead"] = payload.LeadId;
                tokens["value"] = FormatCurrency(payload.ValueUsd);
                return (CueKinds.DealClosed, tokens, payload.Persona, payload.LeadId);
            }
            case ArenaEventKinds.GlengarryLeadDripped:
            {
                var payload = evt.GetPayload<GlengarryLeadDrippedPayload>();
                if (payload is null) return (null, EmptyTokens, null, null);
                tokens["persona"] = payload.Persona;
                tokens["count"] = payload.LeadIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                tokens["reason"] = payload.Reason;
                return (CueKinds.GlengarryDripped, tokens, payload.Persona, null);
            }
            case ArenaEventKinds.LeadsRevoked:
            {
                var payload = evt.GetPayload<LeadsRevokedPayload>();
                if (payload is null) return (null, EmptyTokens, null, null);
                tokens["persona"] = payload.Persona;
                tokens["count"] = payload.LeadIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                tokens["reason"] = payload.Reason;
                return (CueKinds.PersonaDropped, tokens, payload.Persona, null);
            }
            case ArenaEventKinds.BellRung:
            {
                var payload = evt.GetPayload<BellRungPayload>();
                if (payload is null) return (null, EmptyTokens, null, null);
                tokens["persona"] = payload.Persona ?? "the floor";
                tokens["reason"] = payload.Reason;
                if (payload.NarrationLine is not null)
                {
                    tokens["line"] = payload.NarrationLine;
                }
                return (CueKinds.BellRung, tokens, payload.Persona, payload.LeadId);
            }
            default:
                return (null, EmptyTokens, null, null);
        }
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyTokens =
        new Dictionary<string, string>(0, StringComparer.Ordinal);

    private static string FormatCurrency(decimal? value)
    {
        if (value is null)
        {
            return "an undisclosed amount";
        }
        return "$" + value.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Dispatch a synthetic PersonaPromoted cue. Not yet wired to a ledger
    /// event kind (SA-08-17 The Bench will introduce PersonaPromoted /
    /// PersonaRelegated event kinds); this seam lets the bench manager
    /// drive the cue once it ships.
    /// </summary>
    public Task<(CueDispatchOutcome Outcome, ArenaCue? Cue)> AnnouncePromotionAsync(
        string contestId,
        string persona,
        string? fromTier,
        string? toTier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(contestId);
        ArgumentException.ThrowIfNullOrEmpty(persona);

        var tokens = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["persona"] = persona,
            ["from"] = fromTier ?? "the bench",
            ["to"] = toTier ?? "the floor",
        };

        return DispatchManualAsync(contestId, CueKinds.PersonaPromoted, tokens, persona, leadId: null, cancellationToken);
    }

    private async Task<(CueDispatchOutcome Outcome, ArenaCue? Cue)> DispatchManualAsync(
        string contestId,
        string cueKind,
        IReadOnlyDictionary<string, string> tokens,
        string? persona,
        string? leadId,
        CancellationToken cancellationToken)
    {
        var line = _resolver.Resolve(cueKind, tokens);
        if (line is null)
        {
            return (CueDispatchOutcome.NoTemplate, null);
        }

        var cue = new ArenaCue(
            ContestId: contestId,
            CueKind: cueKind,
            Line: line,
            Persona: persona,
            LeadId: leadId,
            TimestampUtc: _timeProvider.GetUtcNow(),
            Tokens: tokens);

        if (_narrator.IsMuted)
        {
            return (CueDispatchOutcome.Muted, cue);
        }

        if (!_rateLimiter.TryAcquire())
        {
            return (CueDispatchOutcome.RateLimited, cue);
        }

        await _narrator.SpeakAsync(cue, cancellationToken).ConfigureAwait(false);
        return (CueDispatchOutcome.Spoken, cue);
    }
}
