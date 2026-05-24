using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace SalesArena.Calendar;

/// <summary>
/// Minimal RFC 5545 ICS reader for `VEVENT` blocks with `DTSTART` / `DTEND` in UTC.
/// Sufficient for demo calendars; not a complete ICS implementation (no RRULE,
/// timezone components, attachments, or attendee parsing). Throws on malformed
/// blocks rather than silently dropping events.
/// </summary>
public static class IcsAdapter
{
    public static IReadOnlyList<CalendarEvent> Parse(TextReader reader)
    {
        if (reader is null) throw new ArgumentNullException(nameof(reader));
        var events = new List<CalendarEvent>();
        bool inEvent = false;
        string? uid = null, summary = null, organizer = null;
        DateTimeOffset? start = null, end = null;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            if (trimmed.Equals("BEGIN:VEVENT", StringComparison.Ordinal))
            {
                inEvent = true;
                uid = summary = organizer = null;
                start = end = null;
                continue;
            }
            if (trimmed.Equals("END:VEVENT", StringComparison.Ordinal))
            {
                if (!inEvent) throw new FormatException("END:VEVENT without BEGIN:VEVENT.");
                if (uid is null || summary is null || start is null || end is null)
                    throw new FormatException("VEVENT missing required UID/SUMMARY/DTSTART/DTEND.");
                events.Add(new CalendarEvent(uid, summary, start.Value, end.Value, organizer));
                inEvent = false;
                continue;
            }
            if (!inEvent) continue;

            var colon = trimmed.IndexOf(':');
            if (colon < 0) continue;
            var key = trimmed.Substring(0, colon);
            var value = trimmed.Substring(colon + 1);
            var paramSplit = key.IndexOf(';');
            var bareKey = paramSplit < 0 ? key : key.Substring(0, paramSplit);

            switch (bareKey)
            {
                case "UID":      uid = value; break;
                case "SUMMARY":  summary = value; break;
                case "ORGANIZER": organizer = value; break;
                case "DTSTART":  start = ParseUtc(value); break;
                case "DTEND":    end = ParseUtc(value); break;
            }
        }

        if (inEvent) throw new FormatException("ICS ended inside a VEVENT block.");
        return events;
    }

    public static IReadOnlyList<CalendarEvent> Parse(string ics)
    {
        if (ics is null) throw new ArgumentNullException(nameof(ics));
        using var reader = new StringReader(ics);
        return Parse(reader);
    }

    private static DateTimeOffset ParseUtc(string value)
    {
        // RFC 5545 UTC form: YYYYMMDDTHHMMSSZ
        if (DateTimeOffset.TryParseExact(value, "yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
            return dto;
        throw new FormatException($"Could not parse DTSTART/DTEND value '{value}' as UTC datetime.");
    }
}
