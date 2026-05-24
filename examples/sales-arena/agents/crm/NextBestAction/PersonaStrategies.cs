using System.Collections.Generic;

namespace SalesArena.Crm.NextBestAction;

public interface IPersonaStrategy
{
    string PersonaId { get; }
    INbaNode BuildTree();
}

/// <summary>Roma — consultative; defaults to Discover/Qualify before pitching.</summary>
public sealed class RomaStrategy : IPersonaStrategy
{
    public string PersonaId => "roma";

    public INbaNode BuildTree() => new Selector("roma-root",
        new Condition("disqualify-low-fit",
            c => c.FitScore < 20,
            NbaActionType.Disqualify,
            "Fit < 20; consultative seller does not waste cycles."),
        new Condition("resolve-objections-first",
            c => c.HasOpenObjections,
            NbaActionType.SendFollowUp,
            "Open objections take priority; send tailored follow-up."),
        new Condition("close-when-ready",
            c => c.FitScore >= 70 && c.IntentScore >= 70 && c.PowerScore >= 50 && c.PendingProposalAmountUsd > 0,
            NbaActionType.NegotiateClose,
            "Strong qualification + proposal out — close consultatively."),
        new Condition("schedule-when-warm",
            c => c.IntentScore >= 60 && !c.HasMeetingScheduled,
            NbaActionType.ScheduleMeeting,
            "Warm intent and no meeting on books — book one."),
        new FallbackAction("default-discover", NbaActionType.Discover, "Default consultative play: more discovery."));
}

/// <summary>Levene — talker; over-indexes on activity, prefers Demo/Pitch and SendFollowUp.</summary>
public sealed class LeveneStrategy : IPersonaStrategy
{
    public string PersonaId => "levene";

    public INbaNode BuildTree() => new Selector("levene-root",
        new Condition("re-engage-cold",
            c => c.DaysSinceLastTouch >= 7,
            NbaActionType.SendFollowUp,
            "Talker pattern: any silence > 1 week gets a follow-up touch."),
        new Condition("pitch-on-any-signal",
            c => c.IntentScore >= 40,
            NbaActionType.DemoOrPitch,
            "Talker pattern: any intent signal → push the pitch."),
        new Condition("propose-when-late-stage",
            c => c.Stage is "Negotiation" or "Proposal",
            NbaActionType.SendProposal,
            "Late stage; throw the proposal again."),
        new FallbackAction("default-followup", NbaActionType.SendFollowUp, "When in doubt, send another follow-up."));
}

/// <summary>Moss — hardballer; aggressive close-or-disqualify posture.</summary>
public sealed class MossStrategy : IPersonaStrategy
{
    public string PersonaId => "moss";

    public INbaNode BuildTree() => new Selector("moss-root",
        new Condition("aggressive-disqualify",
            c => c.FitScore < 35 || c.PowerScore < 20,
            NbaActionType.Disqualify,
            "Hardballer: weak fit or no power — cut the lead."),
        new Condition("close-now-if-late-stage",
            c => c.Stage is "Negotiation" && c.PendingProposalAmountUsd > 0,
            NbaActionType.NegotiateClose,
            "Late stage with proposal out — push the close."),
        new Condition("force-meeting-if-power-present",
            c => c.PowerScore >= 50 && !c.HasMeetingScheduled,
            NbaActionType.ScheduleMeeting,
            "Power identified; force a meeting on the calendar."),
        new Condition("demo-when-any-fit",
            c => c.FitScore >= 50,
            NbaActionType.DemoOrPitch,
            "Skip discovery; demo first to test interest."),
        new FallbackAction("default-qualify", NbaActionType.Qualify, "Cold or unclear — push qualifying questions hard."));
}

/// <summary>Williamson — rule-follower; never skips a stage.</summary>
public sealed class WilliamsonStrategy : IPersonaStrategy
{
    public string PersonaId => "williamson";

    public INbaNode BuildTree() => new Selector("williamson-root",
        new Condition("stage-discover",
            c => c.Stage is "New" or "Discovery",
            NbaActionType.Discover,
            "Process says: do discovery before anything else."),
        new Condition("stage-qualify",
            c => c.Stage is "Qualification",
            NbaActionType.Qualify,
            "Process says: complete qualification before demo."),
        new Condition("stage-demo",
            c => c.Stage is "Demo" or "Evaluation",
            NbaActionType.DemoOrPitch,
            "Process says: run the demo step in order."),
        new Condition("stage-propose",
            c => c.Stage is "Proposal" && c.PendingProposalAmountUsd is null,
            NbaActionType.SendProposal,
            "Process says: send the proposal artifact."),
        new Condition("stage-negotiate",
            c => c.Stage is "Negotiation",
            NbaActionType.NegotiateClose,
            "Process says: negotiate when stage says so."),
        new Condition("wait-when-meeting-on-books",
            c => c.HasMeetingScheduled,
            NbaActionType.Wait,
            "Process says: do not double-touch when a meeting is on the books."),
        new FallbackAction("default-followup", NbaActionType.SendFollowUp, "No clean rule branch — schedule a follow-up touch."));
}

public static class PersonaStrategies
{
    public static IReadOnlyDictionary<string, IPersonaStrategy> Defaults { get; } =
        new Dictionary<string, IPersonaStrategy>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["roma"] = new RomaStrategy(),
            ["levene"] = new LeveneStrategy(),
            ["moss"] = new MossStrategy(),
            ["williamson"] = new WilliamsonStrategy(),
        };
}
