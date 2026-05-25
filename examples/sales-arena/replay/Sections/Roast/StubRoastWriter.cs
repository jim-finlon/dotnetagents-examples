using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.Sections.Roast;

/// <summary>
/// Deterministic, no-LLM stub for tests + offline demo-mode. Reads the
/// target's loss + missed-touch events and synthesizes a 2-3 sentence roast
/// keyed off the roaster's voice. Always includes citations.
///
/// <para>Real LLM-backed writers replace this at runtime by registering a
/// different IRoastWriter; the section builder + hallucination guard don't
/// care which writer is wired in.</para>
/// </summary>
public sealed class StubRoastWriter : IRoastWriter
{
    public Task<string> WriteRoastAsync(
        string roaster,
        string target,
        IReadOnlyList<ArenaEvent> targetEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roaster);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(targetEvents);

        // Anchor events: prefer a Lost DealClosed, then any Objection, then any TouchSent.
        var loss = targetEvents.FirstOrDefault(IsLostDeal);
        var anchor = loss ?? targetEvents.FirstOrDefault(e => e.Kind == ArenaEventKinds.Objection)
                          ?? targetEvents.FirstOrDefault(e => e.Kind == ArenaEventKinds.TouchSent);

        var voice = RoasterVoiceMap.For(roaster);
        var sb = new StringBuilder();

        if (anchor is null)
        {
            // Nothing concrete to cite — produce a structured "no record" line that still passes the guard.
            sb.Append(VoicedOpening(voice, roaster, target));
            sb.Append($" The ledger gives me no Lost or Objection events to point at — quiet contest, quiet roast. [evt:none]");
            return Task.FromResult(sb.ToString());
        }

        sb.Append(VoicedOpening(voice, roaster, target));
        sb.Append(' ');
        sb.Append(VoicedMiddle(voice, anchor));
        sb.Append($" [evt:{anchor.Id}]");
        sb.Append(' ');
        sb.Append(VoicedClose(voice, target));

        return Task.FromResult(sb.ToString());
    }

    private static bool IsLostDeal(ArenaEvent evt)
    {
        if (evt.Kind != ArenaEventKinds.DealClosed) return false;
        var payload = evt.GetPayload<DealClosedPayload>();
        return payload is not null && string.Equals(payload.Outcome, "Lost", StringComparison.OrdinalIgnoreCase);
    }

    private static string VoicedOpening(RoasterVoice voice, string roaster, string target) => voice switch
    {
        RoasterVoice.Elegant => $"A toast — to **{target}**, who fought the good fight.",
        RoasterVoice.Blunt => $"**{target}**, let's talk.",
        RoasterVoice.Surgical => $"Reviewing the tape on **{target}** — strictly the facts.",
        RoasterVoice.Steady => $"Hey **{target}**, just want to share a moment from the day.",
        _ => $"**{target}**:",
    };

    private static string VoicedMiddle(RoasterVoice voice, ArenaEvent anchor)
    {
        var when = anchor.OccurredAtUtc.ToString("u", CultureInfo.InvariantCulture);
        var leadHint = string.IsNullOrEmpty(anchor.LeadId) ? "a prospect" : $"`{anchor.LeadId}`";

        return voice switch
        {
            RoasterVoice.Elegant => $"The {anchor.Kind} on {leadHint} at {when} was, shall we say, *aspirational*.",
            RoasterVoice.Blunt => $"Your {anchor.Kind} on {leadHint} at {when}? Brutal. Won't happen twice.",
            RoasterVoice.Surgical => $"Specifically the {anchor.Kind} on {leadHint} at {when} — that's where the pattern broke.",
            RoasterVoice.Steady => $"There was that {anchor.Kind} on {leadHint} at {when} — could've gone either way.",
            _ => $"Look at the {anchor.Kind} on {leadHint} at {when}.",
        };
    }

    private static string VoicedClose(RoasterVoice voice, string target) => voice switch
    {
        RoasterVoice.Elegant => "Tomorrow, the wind turns. We'll be watching.",
        RoasterVoice.Blunt => "Get it together. Coffee's for closers.",
        RoasterVoice.Surgical => "Run it back with the counter-position pre-loaded.",
        RoasterVoice.Steady => "Take the night. Tomorrow's another batch.",
        _ => "Onward.",
    };
}
