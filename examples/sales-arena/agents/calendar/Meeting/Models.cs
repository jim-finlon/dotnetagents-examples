using System;
using System.Collections.Generic;

namespace SalesArena.Meeting;

public sealed record ProspectId(string Value);

public sealed record Persona(string Id, string DisplayName);

public sealed record CompanyNews(string Headline, DateTimeOffset PublishedAtUtc, string Source);

public sealed record MutualConnection(string Name, string Role, string Path);

public sealed record CrmHistoryEntry(string Stage, DateTimeOffset AtUtc, string Note);

public sealed record TalkingPoint(string Topic, string Detail);

public sealed record Brief(
    ProspectId ProspectId,
    Persona Persona,
    IReadOnlyList<CompanyNews> CompanyNews,
    IReadOnlyList<MutualConnection> MutualConnections,
    IReadOnlyList<CrmHistoryEntry> CrmHistory,
    IReadOnlyList<TalkingPoint> TalkingPoints);

public sealed record TranscriptTurn(string Speaker, string Text, TimeSpan At);

public sealed record MeetingTranscript(
    ProspectId ProspectId,
    DateTimeOffset StartedAtUtc,
    IReadOnlyList<TranscriptTurn> Turns);

public sealed record Decision(string Text, string Owner);
public sealed record ActionItem(string Text, string Owner, DateTimeOffset? Due);
public sealed record Objection(string Text, string Speaker);
public sealed record NextStep(string Text, DateTimeOffset? Due);
public sealed record SentimentShift(string From, string To, TimeSpan At);

public sealed record MeetingSummary(
    ProspectId ProspectId,
    IReadOnlyList<Decision> Decisions,
    IReadOnlyList<ActionItem> Actions,
    IReadOnlyList<Objection> Objections,
    IReadOnlyList<NextStep> NextSteps,
    IReadOnlyList<SentimentShift> SentimentShifts);

public sealed record CrmStageChangeEvent(
    ProspectId ProspectId,
    string FromStage,
    string ToStage,
    DateTimeOffset AtUtc,
    string Source);
