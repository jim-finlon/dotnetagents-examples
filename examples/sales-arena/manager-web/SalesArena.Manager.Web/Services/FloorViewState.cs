using System.Text.Json;
using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Models;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Manager.Web.Services;

/// <summary>
/// Projects ledger events into per-persona floor cards for the manager grid.
/// </summary>
public sealed class FloorViewState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan PulseDuration = TimeSpan.FromSeconds(2);

    private readonly Dictionary<string, PersonaFloorCardModel> _personas = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _pulseUntil = new(StringComparer.OrdinalIgnoreCase);
    private long _lastAppliedEventId;
    private bool _wired;
    private ArenaLiveFeed? _feed;

    public event Action? StateChanged;

    public IReadOnlyList<PersonaFloorCardModel> Personas =>
        _personas.Values.OrderBy(p => p.DisplayName).ToList();

    public void EnsureDefaultPods()
    {
        if (_personas.Count > 0)
        {
            return;
        }

        foreach (var pod in FloorPersonaCatalog.DefaultPods)
        {
            _personas[pod.PersonaId] = pod;
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
        EnsureDefaultPods();
        var ordered = events.Where(e => e.Id > _lastAppliedEventId).OrderBy(e => e.Id).ToList();
        if (ordered.Count == 0)
        {
            RefreshPulseFlags();
            return;
        }

        foreach (var evt in ordered)
        {
            ApplyEvent(evt);
            _lastAppliedEventId = Math.Max(_lastAppliedEventId, evt.Id);
        }

        RefreshPulseFlags();
        StateChanged?.Invoke();
    }

    private void ApplyEvent(ArenaEventMessage evt)
    {
        switch (evt.Kind)
        {
            case ArenaEventKinds.LeadResearched when evt.Persona is not null:
                UpdatePersona(evt.Persona, p => p.With(activity: FloorActivity.Researching), FormatTicker(evt, "Research complete"));
                break;
            case ArenaEventKinds.TouchSent when evt.Persona is not null:
                UpdatePersona(
                    evt.Persona,
                    p => p.With(activity: FloorActivity.Sending, touchesToday: p.TouchesToday + 1),
                    FormatTicker(evt, "Touch sent"));
                break;
            case ArenaEventKinds.InboundReceived when evt.Persona is not null:
                UpdatePersona(
                    evt.Persona,
                    p => p.With(activity: FloorActivity.Waiting, repliesToday: p.RepliesToday + 1),
                    FormatTicker(evt, "Inbound reply"));
                break;
            case ArenaEventKinds.MeetingBooked when evt.Persona is not null:
                UpdatePersona(
                    evt.Persona,
                    p => p.With(activity: FloorActivity.InMeeting, meetingsToday: p.MeetingsToday + 1),
                    FormatTicker(evt, "Meeting booked"));
                break;
            case ArenaEventKinds.MeetingHeld when evt.Persona is not null:
                UpdatePersona(
                    evt.Persona,
                    p => p.With(activity: FloorActivity.Waiting, meetingsToday: p.MeetingsToday + 1),
                    FormatTicker(evt, "Meeting held"));
                break;
            case ArenaEventKinds.ProposalSent when evt.Persona is not null:
                UpdatePersona(evt.Persona, p => p.With(activity: FloorActivity.Drafting), FormatTicker(evt, "Proposal sent"));
                break;
            case ArenaEventKinds.DealClosed when evt.Persona is not null:
                _pulseUntil[evt.Persona] = DateTimeOffset.UtcNow.Add(PulseDuration);
                UpdatePersona(
                    evt.Persona,
                    p => p.With(activity: FloorActivity.Waiting, dealsToday: p.DealsToday + 1, pulseDealClosed: true),
                    FormatTicker(evt, "Deal closed"));
                break;
            case ArenaEventKinds.LeaderboardSnapshot:
                ApplyLeaderboardSnapshot(evt);
                break;
        }
    }

    private void ApplyLeaderboardSnapshot(ArenaEventMessage evt)
    {
        var payload = Deserialize<LeaderboardSnapshotPayload>(evt.PayloadJson);
        if (payload is null)
        {
            return;
        }

        foreach (var entry in payload.Entries)
        {
            var tier = MapTier(entry.Tier);
            UpdatePersona(entry.Persona, p => p.With(tier: tier), $"{entry.Tier} board · #{entry.Position}");
        }
    }

    private void UpdatePersona(string personaId, Func<PersonaFloorCardModel, PersonaFloorCardModel> mutate, string tickerLine)
    {
        if (!_personas.TryGetValue(personaId, out var current))
        {
            current = new PersonaFloorCardModel
            {
                PersonaId = personaId,
                DisplayName = char.ToUpperInvariant(personaId[0]) + personaId[1..],
                AvatarGlyph = personaId[..1].ToUpperInvariant(),
            };
        }

        var next = mutate(current);
        var ticker = PrependTicker(next.TickerLines, tickerLine);
        _personas[personaId] = next.With(tickerLines: ticker);
    }

    private static IReadOnlyList<string> PrependTicker(IReadOnlyList<string> existing, string line)
    {
        var list = new List<string> { line };
        list.AddRange(existing.Take(4));
        return list;
    }

    private static string FormatTicker(ArenaEventMessage evt, string summary) =>
        $"{evt.OccurredAtUtc.LocalDateTime:t} · {summary}";

    private void RefreshPulseFlags()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (personaId, model) in _personas.ToList())
        {
            var pulsing = _pulseUntil.TryGetValue(personaId, out var until) && until > now;
            if (model.PulseDealClosed != pulsing)
            {
                _personas[personaId] = model.With(pulseDealClosed: pulsing);
            }
        }
    }

    private static FloorTier MapTier(string tierName) =>
        tierName switch
        {
            LeaderboardTierNames.Cadillac => FloorTier.Cadillac,
            LeaderboardTierNames.YouAreFired => FloorTier.YouAreFired,
            _ => FloorTier.SteakKnives,
        };

    private static T? Deserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
