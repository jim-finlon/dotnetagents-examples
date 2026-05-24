using System;
using System.Collections.Generic;

namespace SalesArena.Orchestrator.Contest;

public enum ContestPhase
{
    Uninitialized = 0,
    Initialized = 1,
    Running = 2,
    Paused = 3,
    Ended = 4,
}

public sealed record ContestConfig(
    string Name,
    string LeadsPackRef,
    IReadOnlyList<string> PersonaIds,
    double DurationHours,
    string PrizeTier,
    double TimeCompressionFactor = 1.0);

public sealed record ContestState(
    string ContestId,
    string Name,
    ContestPhase Phase,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? PausedAtUtc,
    DateTimeOffset? EndedAtUtc,
    TimeSpan AccumulatedSimulatedRunTime,
    IReadOnlyList<string> ActivePersonaIds,
    double TimeCompressionFactor,
    string PrizeTier);

public sealed record ContestPhaseChangedEvent(
    string ContestId,
    ContestPhase FromPhase,
    ContestPhase ToPhase,
    DateTimeOffset AtUtc,
    string? Reason = null);

public sealed record LeaderboardEntry(string PersonaId, int Score);
