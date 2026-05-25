using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Training.Diary;

/// <summary>
/// Deterministic, no-LLM stub. Produces a 150-word ± 30 in-voice journal
/// entry citing the day's two most consequential events. Used by tests and
/// the offline demo mode; real LLM-backed writers replace it via DI.
/// </summary>
public sealed class StubDiaryWriter : IDiaryWriter
{
    private const int TargetMinWords = 120;
    private const int TargetMaxWords = 180;

    public Task<string> WriteEntryAsync(DiaryDayContext day, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(day);

        var topEvents = PickTopEvents(day.Events, count: 3);
        var moraleTone = ResolveMorale(day);
        var sb = new StringBuilder();

        sb.AppendLine(WriteOpening(day, moraleTone));
        sb.AppendLine();
        sb.AppendLine(WriteMiddle(day, moraleTone, topEvents));
        sb.AppendLine();
        sb.AppendLine(WriteClose(day, moraleTone));

        var body = sb.ToString().Trim();
        body = PadToWordCount(body, minWords: TargetMinWords, maxWords: TargetMaxWords);
        return Task.FromResult(body);
    }

    private static IReadOnlyList<ArenaEvent> PickTopEvents(IReadOnlyList<ArenaEvent> events, int count)
    {
        if (events.Count == 0) return Array.Empty<ArenaEvent>();

        // Priority: DealClosed Won > DealClosed Lost > MeetingHeld > ProposalSent > others.
        static int Rank(ArenaEvent e) => e.Kind switch
        {
            ArenaEventKinds.DealClosed => IsWon(e) ? 0 : 1,
            ArenaEventKinds.MeetingHeld => 2,
            ArenaEventKinds.ProposalSent => 3,
            ArenaEventKinds.MeetingBooked => 4,
            ArenaEventKinds.InboundReceived => 5,
            _ => 6,
        };
        return events.OrderBy(Rank).ThenBy(e => e.OccurredAtUtc).Take(count).ToList();
    }

    private static bool IsWon(ArenaEvent evt)
    {
        if (evt.Kind != ArenaEventKinds.DealClosed) return false;
        var p = evt.GetPayload<DealClosedPayload>();
        return p is not null && string.Equals(p.Outcome, "Won", StringComparison.OrdinalIgnoreCase);
    }

    private static Morale ResolveMorale(DiaryDayContext day)
    {
        var thirdMark = Math.Max(1, day.TotalPositions / 3);
        if (day.LeaderboardPosition <= thirdMark) return Morale.Soaring;
        if (day.LeaderboardPosition <= 2 * thirdMark) return Morale.Steady;
        return Morale.Bruised;
    }

    private static string WriteOpening(DiaryDayContext day, Morale tone)
    {
        var prefix = $"**Day {day.Day} — {day.Persona}.**";
        return tone switch
        {
            Morale.Soaring  => $"{prefix} Position #{day.LeaderboardPosition} of {day.TotalPositions}. The board likes me today and I don't take that lightly.",
            Morale.Steady   => $"{prefix} Holding at #{day.LeaderboardPosition}. Not the headline, but the ledger doesn't lie.",
            Morale.Bruised  => $"{prefix} Position #{day.LeaderboardPosition} hurts to write down. The board is honest even when I don't want it to be.",
            _ => prefix,
        };
    }

    private static string WriteMiddle(DiaryDayContext day, Morale tone, IReadOnlyList<ArenaEvent> topEvents)
    {
        if (topEvents.Count == 0)
        {
            return "Quiet day. No leads moved, no objections raised, no meetings booked. [evt:none] [evt:none] The ledger is empty and so is the takeaway — just rest and reset for tomorrow.";
        }

        var sb = new StringBuilder();
        var revenueLabel = day.RevenueToday > 0
            ? string.Format(CultureInfo.InvariantCulture, "${0:N0} in", day.RevenueToday)
            : "no revenue from";
        sb.Append($"{revenueLabel} {day.DealsClosedToday} closed and {day.DealsLostToday} lost.");

        foreach (var evt in topEvents)
        {
            sb.Append(' ');
            sb.Append(DescribeEvent(evt, tone));
            sb.Append($" [evt:{evt.Id}]");
        }
        return sb.ToString();
    }

    private static string DescribeEvent(ArenaEvent evt, Morale tone)
    {
        var lead = string.IsNullOrEmpty(evt.LeadId) ? "a prospect" : $"`{evt.LeadId}`";
        var when = evt.OccurredAtUtc.ToString("HH:mm", CultureInfo.InvariantCulture);

        return evt.Kind switch
        {
            ArenaEventKinds.DealClosed when IsWon(evt) => tone == Morale.Soaring
                ? $"At {when} {lead} closed; the bell rang and the room knew."
                : $"At {when} {lead} came through — a clean close that bought the day.",
            ArenaEventKinds.DealClosed => tone == Morale.Bruised
                ? $"At {when} {lead} slipped — I read the room wrong and the deal walked."
                : $"At {when} {lead} closed Lost — a useful no, eventually.",
            ArenaEventKinds.MeetingHeld => $"At {when} the meeting with {lead} held; the team showed up and so did I.",
            ArenaEventKinds.ProposalSent => $"At {when} the proposal to {lead} went out — now we wait.",
            ArenaEventKinds.MeetingBooked => $"At {when} I booked {lead}; that's tomorrow's pivot point.",
            ArenaEventKinds.InboundReceived => $"At {when} inbound from {lead} landed — they're thinking about it, which is what we asked for.",
            ArenaEventKinds.TouchSent => $"At {when} a touch went to {lead}; that bottle was thrown out, the ocean will answer.",
            _ => $"At {when} {evt.Kind} on {lead} — small ripple, recorded.",
        };
    }

    private static string WriteClose(DiaryDayContext day, Morale tone) => tone switch
    {
        Morale.Soaring  => "Tomorrow I keep the cadence. The Cadillac stays in my parking spot only if I earn it twice.",
        Morale.Steady   => "Tomorrow I find the one extra touch I left on the table today.",
        Morale.Bruised  => "Tomorrow I open with the lead I'm most afraid of. The way out is through the wall.",
        _ => "Tomorrow continues.",
    };

    /// <summary>
    /// Pad with neutral closing lines (always citation-free) until the word
    /// count clears the floor. We never trim — running over the ceiling is
    /// preferable to losing citations or context the guard relies on.
    /// </summary>
    private static string PadToWordCount(string body, int minWords, int maxWords)
    {
        var current = CountWords(body);
        if (current >= minWords) return body;

        var padding = new List<string>
        {
            "I'm writing this down because a habit only counts when you can read it back.",
            "The board doesn't care how I felt; it cares what I shipped.",
            "Tomorrow is just another set of touches, taken one at a time.",
            "Coffee's for closers — and discipline is for everyone else.",
        };

        var sb = new StringBuilder(body);
        foreach (var line in padding)
        {
            if (CountWords(sb.ToString()) >= minWords) break;
            sb.Append(' ');
            sb.Append(line);
        }
        return sb.ToString();
    }

    private static int CountWords(string body) =>
        body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private enum Morale { Soaring, Steady, Bruised }
}
