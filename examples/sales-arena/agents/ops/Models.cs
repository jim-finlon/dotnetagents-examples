using System.Collections.Generic;

namespace SalesArena.Ops;

public sealed record ContestRequest(
    string Name,
    IReadOnlyList<string> PersonaIds,
    double DurationHours,
    string PrizeTier,
    System.DateTimeOffset? StartUtc = null);

public sealed record ContestPlan(
    string Name,
    System.DateTimeOffset StartUtc,
    System.DateTimeOffset EndUtc,
    int PersonaSeatCount,
    string PrizeTier,
    IReadOnlyList<string> PersonaIds);

public sealed record BuildQueueRequest(
    string PersonaId,
    IReadOnlyList<string> LeadHandles,
    int OptCap);

public sealed record QueueItem(string LeadHandle, int Position);

public sealed record DailyQueue(
    string PersonaId,
    IReadOnlyList<QueueItem> Items);
