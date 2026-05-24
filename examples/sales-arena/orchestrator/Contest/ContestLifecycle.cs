using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesArena.Orchestrator.Contest;

public interface IContestLifecycle
{
    string Init(ContestConfig config);
    void Start(string contestId);
    void Pause(string contestId);
    void Resume(string contestId);
    void End(string contestId);
    ContestState GetState(string contestId);
    IReadOnlyList<ContestPhaseChangedEvent> GetPhaseLog(string contestId);
    IReadOnlyList<LeaderboardEntry> GetLeaderboard(string contestId);
    void RecordScore(string contestId, string personaId, int delta);
}

/// <summary>
/// In-process, deterministic contest lifecycle. Persists phase changes and leaderboard
/// to in-memory ledgers; production wiring (SA-02-03 ArenaLedger) substitutes those
/// ledgers without changing the state machine. Pause/resume preserves leaderboard
/// (scores are not zeroed). Time compression factor multiplies simulated time during
/// running phases for demo replay (1.0 default, 60.0 typical demo).
/// </summary>
public sealed class ContestLifecycle : IContestLifecycle
{
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Dictionary<string, ContestState> _states = new();
    private readonly Dictionary<string, List<ContestPhaseChangedEvent>> _phaseLog = new();
    private readonly Dictionary<string, Dictionary<string, int>> _leaderboards = new();

    public ContestLifecycle() : this(() => DateTimeOffset.UtcNow) { }

    public ContestLifecycle(Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public string Init(ContestConfig config)
    {
        if (config is null) throw new ArgumentNullException(nameof(config));
        if (string.IsNullOrWhiteSpace(config.Name))
            throw new ArgumentException("Contest name is required.", nameof(config));
        if (string.IsNullOrWhiteSpace(config.LeadsPackRef))
            throw new ArgumentException("LeadsPackRef is required.", nameof(config));
        if (config.PersonaIds is null || config.PersonaIds.Count == 0)
            throw new ArgumentException("At least one persona is required.", nameof(config));
        if (config.DurationHours <= 0)
            throw new ArgumentException("DurationHours must be > 0.", nameof(config));
        if (config.TimeCompressionFactor <= 0)
            throw new ArgumentException("TimeCompressionFactor must be > 0.", nameof(config));

        var personas = config.PersonaIds
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToArray();
        if (personas.Length == 0)
            throw new ArgumentException("At least one non-blank persona is required.", nameof(config));

        var contestId = Guid.NewGuid().ToString("N");
        var state = new ContestState(
            ContestId: contestId,
            Name: config.Name.Trim(),
            Phase: ContestPhase.Initialized,
            StartedAtUtc: null,
            PausedAtUtc: null,
            EndedAtUtc: null,
            AccumulatedSimulatedRunTime: TimeSpan.Zero,
            ActivePersonaIds: personas,
            TimeCompressionFactor: config.TimeCompressionFactor,
            PrizeTier: string.IsNullOrWhiteSpace(config.PrizeTier) ? "standard" : config.PrizeTier.Trim());

        _states[contestId] = state;
        _phaseLog[contestId] = new List<ContestPhaseChangedEvent>
        {
            new(contestId, ContestPhase.Uninitialized, ContestPhase.Initialized, _utcNow(), "init"),
        };
        _leaderboards[contestId] = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in personas) _leaderboards[contestId][p] = 0;

        return contestId;
    }

    public void Start(string contestId)
    {
        var state = Require(contestId);
        if (state.Phase != ContestPhase.Initialized)
            throw new InvalidOperationException($"Cannot Start from phase {state.Phase}.");
        var now = _utcNow();
        _states[contestId] = state with { Phase = ContestPhase.Running, StartedAtUtc = now };
        Log(contestId, ContestPhase.Initialized, ContestPhase.Running, now, "start");
    }

    public void Pause(string contestId)
    {
        var state = Require(contestId);
        if (state.Phase != ContestPhase.Running)
            throw new InvalidOperationException($"Cannot Pause from phase {state.Phase}.");
        var now = _utcNow();
        var simulatedRun = SimulatedRunSince(state, now);
        _states[contestId] = state with
        {
            Phase = ContestPhase.Paused,
            PausedAtUtc = now,
            AccumulatedSimulatedRunTime = state.AccumulatedSimulatedRunTime + simulatedRun,
        };
        Log(contestId, ContestPhase.Running, ContestPhase.Paused, now, "pause");
    }

    public void Resume(string contestId)
    {
        var state = Require(contestId);
        if (state.Phase != ContestPhase.Paused)
            throw new InvalidOperationException($"Cannot Resume from phase {state.Phase}.");
        var now = _utcNow();
        _states[contestId] = state with { Phase = ContestPhase.Running, StartedAtUtc = now, PausedAtUtc = null };
        Log(contestId, ContestPhase.Paused, ContestPhase.Running, now, "resume");
    }

    public void End(string contestId)
    {
        var state = Require(contestId);
        if (state.Phase == ContestPhase.Ended)
            throw new InvalidOperationException("Contest already Ended.");
        if (state.Phase == ContestPhase.Uninitialized)
            throw new InvalidOperationException("Contest is Uninitialized.");
        var now = _utcNow();
        var accumulated = state.AccumulatedSimulatedRunTime;
        if (state.Phase == ContestPhase.Running)
            accumulated += SimulatedRunSince(state, now);
        _states[contestId] = state with { Phase = ContestPhase.Ended, EndedAtUtc = now, AccumulatedSimulatedRunTime = accumulated };
        Log(contestId, state.Phase, ContestPhase.Ended, now, "end");
    }

    public ContestState GetState(string contestId) => Require(contestId);

    public IReadOnlyList<ContestPhaseChangedEvent> GetPhaseLog(string contestId)
    {
        if (!_phaseLog.TryGetValue(contestId, out var log))
            throw new KeyNotFoundException(contestId);
        return log.AsReadOnly();
    }

    public IReadOnlyList<LeaderboardEntry> GetLeaderboard(string contestId)
    {
        if (!_leaderboards.TryGetValue(contestId, out var scores))
            throw new KeyNotFoundException(contestId);
        return scores
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new LeaderboardEntry(kv.Key, kv.Value))
            .ToArray();
    }

    public void RecordScore(string contestId, string personaId, int delta)
    {
        var state = Require(contestId);
        if (state.Phase != ContestPhase.Running)
            throw new InvalidOperationException($"Cannot RecordScore from phase {state.Phase}.");
        if (!_leaderboards[contestId].ContainsKey(personaId))
            throw new ArgumentException($"Persona {personaId} not in active set.", nameof(personaId));
        _leaderboards[contestId][personaId] += delta;
    }

    private ContestState Require(string contestId)
    {
        if (string.IsNullOrWhiteSpace(contestId)) throw new ArgumentException("contestId required", nameof(contestId));
        if (!_states.TryGetValue(contestId, out var s)) throw new KeyNotFoundException(contestId);
        return s;
    }

    private void Log(string contestId, ContestPhase from, ContestPhase to, DateTimeOffset at, string reason)
    {
        _phaseLog[contestId].Add(new ContestPhaseChangedEvent(contestId, from, to, at, reason));
    }

    private static TimeSpan SimulatedRunSince(ContestState state, DateTimeOffset now)
    {
        if (state.StartedAtUtc is null) return TimeSpan.Zero;
        var wall = now - state.StartedAtUtc.Value;
        return TimeSpan.FromTicks((long)(wall.Ticks * state.TimeCompressionFactor));
    }
}
