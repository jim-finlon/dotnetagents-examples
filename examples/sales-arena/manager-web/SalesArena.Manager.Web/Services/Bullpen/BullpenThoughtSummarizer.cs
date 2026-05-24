using SalesArena.Manager.Web.Hubs;
using SalesArena.Manager.Web.Models;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Manager.Web.Services.Bullpen;

/// <summary>
/// Deterministic one-sentence "current thought" lines from ledger events (no live LLM).
/// </summary>
public static class BullpenThoughtSummarizer
{
    public static string SummarizeFromEvent(ArenaEventMessage evt)
    {
        var raw = evt.Kind switch
        {
            ArenaEventKinds.LeadResearched => "Digging into signals before the next touch.",
            ArenaEventKinds.TouchSent => SummarizeTouch(evt),
            ArenaEventKinds.InboundReceived => "Inbound just landed — prioritizing the reply.",
            ArenaEventKinds.MeetingBooked => "Locking a demo slot on the calendar.",
            ArenaEventKinds.MeetingHeld => "Debriefing the meeting and lining up next steps.",
            ArenaEventKinds.ProposalSent => "Polishing numbers before the proposal goes out.",
            ArenaEventKinds.Objection => "Working through an objection with a playbook response.",
            ArenaEventKinds.DealClosed => SummarizeDeal(evt),
            ArenaEventKinds.LeadAssigned => "A fresh lead just hit the queue.",
            _ => "Staying ready for the next play.",
        };

        return BullpenThoughtSanitizer.SanitizePublicThought(raw);
    }

    public static string IdleThought(FloorActivity activity) =>
        BullpenThoughtSanitizer.SanitizePublicThought(activity switch
        {
            FloorActivity.Researching => "Still researching — looking for the angle.",
            FloorActivity.Drafting => "Drafting outreach in the bullpen.",
            FloorActivity.Sending => "Sending touches and watching replies.",
            FloorActivity.Waiting => "Waiting on the prospect to respond.",
            FloorActivity.InMeeting => "In a live meeting — back on the floor soon.",
            _ => "Watching the board for the next move.",
        });

    private static string SummarizeTouch(ArenaEventMessage evt)
    {
        var payload = Deserialize<TouchSentPayload>(evt.PayloadJson);
        var channel = payload?.Channel ?? "email";
        return $"Sending a {channel.ToLowerInvariant()} touch to keep momentum.";
    }

    private static string SummarizeDeal(ArenaEventMessage evt)
    {
        var payload = Deserialize<DealClosedPayload>(evt.PayloadJson);
        var bucket = BullpenThoughtSanitizer.BucketDealValue(payload?.ValueUsd);
        var outcome = string.Equals(payload?.Outcome, "Won", StringComparison.OrdinalIgnoreCase)
            ? "Won"
            : "Closed";
        return $"{outcome} a deal in the {bucket} range.";
    }

    private static T? Deserialize<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    }
}
