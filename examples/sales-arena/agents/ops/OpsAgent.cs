using System;
using System.Collections.Generic;
using System.Linq;

namespace SalesArena.Ops;

/// <summary>
/// Deterministic implementation of <see cref="IOpsAgent"/>. No LLM, no external HTTP.
/// Used by the Sales Arena demo to plan contests and produce a per-rep daily queue.
/// </summary>
public sealed class OpsAgent : IOpsAgent
{
    private readonly Func<DateTimeOffset> _utcNow;

    public OpsAgent() : this(() => DateTimeOffset.UtcNow) { }

    public OpsAgent(Func<DateTimeOffset> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    public ContestPlan SetupContest(ContestRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Contest name is required.", nameof(request));
        if (request.PersonaIds is null || request.PersonaIds.Count == 0)
            throw new ArgumentException("At least one persona is required.", nameof(request));
        if (request.DurationHours <= 0)
            throw new ArgumentException("DurationHours must be > 0.", nameof(request));

        var personas = request.PersonaIds
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToArray();
        if (personas.Length == 0)
            throw new ArgumentException("At least one non-blank persona is required.", nameof(request));

        var start = request.StartUtc ?? _utcNow();
        var end = start.AddHours(request.DurationHours);
        var prizeTier = string.IsNullOrWhiteSpace(request.PrizeTier) ? "standard" : request.PrizeTier.Trim();

        return new ContestPlan(
            Name: request.Name.Trim(),
            StartUtc: start,
            EndUtc: end,
            PersonaSeatCount: personas.Length,
            PrizeTier: prizeTier,
            PersonaIds: personas);
    }

    public DailyQueue BuildDailyQueue(BuildQueueRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.PersonaId))
            throw new ArgumentException("PersonaId is required.", nameof(request));
        if (request.LeadHandles is null)
            throw new ArgumentException("LeadHandles is required.", nameof(request));
        if (request.OptCap < 0)
            throw new ArgumentException("OptCap must be >= 0.", nameof(request));

        var leads = request.LeadHandles
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(h => h, StringComparer.Ordinal)
            .ToArray();

        var capped = request.OptCap == 0
            ? leads
            : leads.Take(request.OptCap).ToArray();

        var items = capped
            .Select((lead, idx) => new QueueItem(lead, idx + 1))
            .ToArray();

        return new DailyQueue(request.PersonaId.Trim(), items);
    }
}
