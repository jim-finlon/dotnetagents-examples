using System.Text.Json;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Manager.Web.Services.Pipeline;

/// <summary>
/// Projects ledger events into funnel stage counts, persona markers, and revenue particles.
/// </summary>
public sealed class PipelineFunnelState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ParticleLifetime = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan TransitionWindow = TimeSpan.FromMinutes(5);

    private readonly int[] _counts = new int[PipelineStageDefinitions.FunnelStages.Count];
    private readonly Dictionary<string, string> _leadStage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _personaStageIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<(DateTimeOffset At, int StageIndex)> _recentTransitions = new();
    private readonly List<PipelineRevenueParticle> _particles = [];
    private readonly Lock _gate = new();

    private long _lastAppliedEventId;
    private decimal _cumulativeRevenueUsd;
    private DateTimeOffset _particleWindowStart = DateTimeOffset.UtcNow;
    private int _particlesInWindow;
    private bool _wired;
    private ArenaLiveFeed? _feed;

    public event Action? StateChanged;

    public decimal CumulativeRevenueUsd
    {
        get
        {
            lock (_gate)
            {
                return _cumulativeRevenueUsd;
            }
        }
    }

    public IReadOnlyList<PipelineStageSnapshot> Stages
    {
        get
        {
            lock (_gate)
            {
                return BuildSnapshots();
            }
        }
    }

    public IReadOnlyList<PipelineRevenueParticle> ActiveParticles
    {
        get
        {
            lock (_gate)
            {
                PruneParticles();
                return _particles.ToList();
            }
        }
    }

    public void WireToFeed(ArenaLiveFeed feed)
    {
        if (_wired)
        {
            return;
        }

        _wired = true;
        _feed = feed;
        feed.EventsChanged += OnFeedEventsChanged;
        ApplyNewEvents(feed.RecentEvents);
    }

    public void UnwireFromFeed(ArenaLiveFeed feed)
    {
        if (!_wired)
        {
            return;
        }

        feed.EventsChanged -= OnFeedEventsChanged;
        _wired = false;
    }

    private void OnFeedEventsChanged()
    {
        if (_feed is not null)
        {
            ApplyNewEvents(_feed.RecentEvents);
        }
    }

    public void ApplyNewEvents(IReadOnlyList<ArenaEventMessage> events)
    {
        var ordered = events.Where(e => e.Id > _lastAppliedEventId).OrderBy(e => e.Id).ToList();
        if (ordered.Count == 0)
        {
            lock (_gate)
            {
                PruneParticles();
            }

            StateChanged?.Invoke();
            return;
        }

        lock (_gate)
        {
            foreach (var evt in ordered)
            {
                ApplyEvent(evt);
                _lastAppliedEventId = Math.Max(_lastAppliedEventId, evt.Id);
            }

            PruneParticles();
        }

        StateChanged?.Invoke();
    }

    private void ApplyEvent(ArenaEventMessage evt)
    {
        var stage = MapEventToStage(evt);
        if (stage is null || evt.Persona is null)
        {
            if (evt.Kind == ArenaEventKinds.DealClosed && evt.Persona is not null)
            {
                TryAddRevenueParticle(evt);
            }

            return;
        }

        if (evt.LeadId is not null)
        {
            if (_leadStage.TryGetValue(evt.LeadId, out var prior) &&
                PipelineStageDefinitions.IndexOf(prior) is var priorIdx and >= 0 &&
                PipelineStageDefinitions.IndexOf(stage) is var nextIdx and >= 0 &&
                nextIdx <= priorIdx)
            {
                return;
            }

            _leadStage[evt.LeadId] = stage;
        }

        AdvancePersona(evt.Persona, stage);
        RecordTransition(stage);

        if (evt.Kind == ArenaEventKinds.DealClosed)
        {
            TryAddRevenueParticle(evt);
        }
    }

    private void AdvancePersona(string persona, string stage)
    {
        var index = PipelineStageDefinitions.IndexOf(stage);
        if (index < 0)
        {
            return;
        }

        if (_personaStageIndex.TryGetValue(persona, out var priorIndex) && priorIndex == index)
        {
            return;
        }

        if (_personaStageIndex.TryGetValue(persona, out priorIndex) && priorIndex >= 0)
        {
            _counts[priorIndex] = Math.Max(0, _counts[priorIndex] - 1);
        }

        _personaStageIndex[persona] = index;
        _counts[index]++;
    }

    private void RecordTransition(string stage)
    {
        var index = PipelineStageDefinitions.IndexOf(stage);
        if (index < 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        _recentTransitions.Enqueue((now, index));
        while (_recentTransitions.Count > 0 && now - _recentTransitions.Peek().At > TransitionWindow)
        {
            _recentTransitions.Dequeue();
        }
    }

    private void TryAddRevenueParticle(ArenaEventMessage evt)
    {
        var payload = Deserialize<DealClosedPayload>(evt.PayloadJson);
        if (payload is null || !string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var value = payload.ValueUsd ?? 0m;
        if (value > 0)
        {
            _cumulativeRevenueUsd += value;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - _particleWindowStart > TimeSpan.FromSeconds(1))
        {
            _particleWindowStart = now;
            _particlesInWindow = 0;
        }

        if (_particlesInWindow >= 3)
        {
            return;
        }

        _particlesInWindow++;
        _particles.Add(new PipelineRevenueParticle(Guid.NewGuid(), value, now));
    }

    private static string? MapEventToStage(ArenaEventMessage evt) =>
        evt.Kind switch
        {
            ArenaEventKinds.LeadAssigned => PipelineStageDefinitions.Lead,
            ArenaEventKinds.LeadResearched => PipelineStageDefinitions.Researched,
            ArenaEventKinds.TouchSent => PipelineStageDefinitions.Contacted,
            ArenaEventKinds.InboundReceived => PipelineStageDefinitions.Qualified,
            ArenaEventKinds.MeetingBooked => PipelineStageDefinitions.DemoBooked,
            ArenaEventKinds.MeetingHeld => PipelineStageDefinitions.DemoHeld,
            ArenaEventKinds.ProposalSent => PipelineStageDefinitions.ProposalSent,
            ArenaEventKinds.Objection => PipelineStageDefinitions.Negotiating,
            ArenaEventKinds.DealClosed => PipelineStageDefinitions.Closed,
            _ => null,
        };

    private void PruneParticles()
    {
        var cutoff = DateTimeOffset.UtcNow - ParticleLifetime;
        _particles.RemoveAll(p => p.CreatedAtUtc < cutoff);
    }

    private IReadOnlyList<PipelineStageSnapshot> BuildSnapshots()
    {
        var transitionCounts = new int[PipelineStageDefinitions.FunnelStages.Count];
        foreach (var (_, stageIndex) in _recentTransitions)
        {
            if (stageIndex >= 0 && stageIndex < transitionCounts.Length)
            {
                transitionCounts[stageIndex]++;
            }
        }

        var personasByStage = _personaStageIndex
            .GroupBy(kv => kv.Value)
            .ToDictionary(g => g.Key, g => g.Select(kv => kv.Key).Take(4).ToList());

        var snapshots = new List<PipelineStageSnapshot>(PipelineStageDefinitions.FunnelStages.Count);
        for (var i = 0; i < PipelineStageDefinitions.FunnelStages.Count; i++)
        {
            var stage = PipelineStageDefinitions.FunnelStages[i];
            var markers = personasByStage.TryGetValue(i, out var personas)
                ? personas.Select(p => new PipelinePersonaMarker(p, stage)).ToList()
                : [];

            snapshots.Add(new PipelineStageSnapshot(
                stage,
                _counts[i],
                transitionCounts[i],
                markers));
        }

        return snapshots;
    }

    private static T? Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
