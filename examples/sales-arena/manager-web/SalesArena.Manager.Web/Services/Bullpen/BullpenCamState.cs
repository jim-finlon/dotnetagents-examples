using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Models;
using SalesArena.Manager.Web.Services;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Manager.Web.Services.Bullpen;

/// <summary>
/// Projects ledger events into per-persona bullpen tiles with sanitized thought bubbles.
/// </summary>
public sealed class BullpenCamState
{
    private readonly Dictionary<string, BullpenTileModel> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private long _lastAppliedEventId;
    private bool _wired;
    private ArenaLiveFeed? _feed;

    public event Action? StateChanged;

    public IReadOnlyList<BullpenTileModel> Tiles =>
        _tiles.Values.OrderBy(t => t.DisplayName).ToList();

    public void EnsureDefaultTiles()
    {
        if (_tiles.Count > 0)
        {
            return;
        }

        foreach (var pod in FloorPersonaCatalog.DefaultPods)
        {
            _tiles[pod.PersonaId] = new BullpenTileModel
            {
                PersonaId = pod.PersonaId,
                DisplayName = pod.DisplayName,
                AvatarGlyph = pod.AvatarGlyph,
                Activity = pod.Activity,
                CurrentThought = BullpenThoughtSummarizer.IdleThought(pod.Activity),
            };
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
        EnsureDefaultTiles();
        var ordered = events.Where(e => e.Id > _lastAppliedEventId).OrderBy(e => e.Id).ToList();
        if (ordered.Count == 0)
        {
            return;
        }

        foreach (var evt in ordered)
        {
            ApplyEvent(evt);
            _lastAppliedEventId = Math.Max(_lastAppliedEventId, evt.Id);
        }

        StateChanged?.Invoke();
    }

    private void ApplyEvent(ArenaEventMessage evt)
    {
        if (evt.Persona is null && evt.Kind != ArenaEventKinds.LeaderboardSnapshot)
        {
            return;
        }

        switch (evt.Kind)
        {
            case ArenaEventKinds.LeadResearched when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Researching, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.TouchSent when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Sending, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.InboundReceived when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Waiting, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.MeetingBooked when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.InMeeting, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.MeetingHeld when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Waiting, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.ProposalSent when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Drafting, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.DealClosed when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Waiting, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.LeadAssigned when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Researching, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.Objection when evt.Persona is not null:
                UpdateTile(evt.Persona, FloorActivity.Drafting, BullpenThoughtSummarizer.SummarizeFromEvent(evt));
                break;
            case ArenaEventKinds.LeaderboardSnapshot:
                ApplyLeaderboardSnapshot(evt);
                break;
        }
    }

    private void ApplyLeaderboardSnapshot(ArenaEventMessage evt)
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<LeaderboardSnapshotPayload>(
            evt.PayloadJson,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        if (payload is null)
        {
            return;
        }

        foreach (var entry in payload.Entries)
        {
            if (!_tiles.TryGetValue(entry.Persona, out var tile))
            {
                continue;
            }

            var thought = BullpenThoughtSanitizer.SanitizePublicThought(
                $"Leaderboard pulse — #{entry.Position} on the {entry.Tier} board.");
            _tiles[entry.Persona] = tile.With(currentThought: thought, updatedAtUtc: evt.OccurredAtUtc);
        }
    }

    private void UpdateTile(string personaId, FloorActivity activity, string thought)
    {
        if (!_tiles.TryGetValue(personaId, out var tile))
        {
            tile = new BullpenTileModel
            {
                PersonaId = personaId,
                DisplayName = char.ToUpperInvariant(personaId[0]) + personaId[1..],
                AvatarGlyph = personaId[..1].ToUpperInvariant(),
            };
        }

        _tiles[personaId] = tile.With(
            activity: activity,
            currentThought: thought,
            updatedAtUtc: DateTimeOffset.UtcNow);
    }
}
