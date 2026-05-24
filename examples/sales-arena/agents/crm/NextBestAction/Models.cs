using System;
using System.Collections.Generic;

namespace SalesArena.Crm.NextBestAction;

public enum NbaActionType
{
    Discover = 0,
    Qualify = 1,
    DemoOrPitch = 2,
    NegotiateClose = 3,
    SendFollowUp = 4,
    ScheduleMeeting = 5,
    SendProposal = 6,
    Wait = 7,
    Disqualify = 8,
}

public sealed record CrmContext(
    string LeadId,
    string Stage,
    int FitScore,
    int IntentScore,
    int PowerScore,
    int DaysSinceLastTouch,
    bool HasOpenObjections,
    bool HasMeetingScheduled,
    decimal? PendingProposalAmountUsd = null);

public sealed record NbaDecision(
    NbaActionType Action,
    string Reason,
    string PersonaId,
    IReadOnlyList<string> Trace);
