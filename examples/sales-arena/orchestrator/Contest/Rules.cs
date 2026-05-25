using System;
using System.Collections.Generic;

namespace SalesArena.Orchestrator.Contest;

public abstract record ContestEvent(string ContestId, DateTimeOffset AtUtc);

public sealed record LeadTouchedEvent(string ContestId, DateTimeOffset AtUtc, string PersonaId, string LeadId) : ContestEvent(ContestId, AtUtc);

public sealed record OutboundSendEvent(string ContestId, DateTimeOffset AtUtc, string PersonaId, int SendCountInWindow) : ContestEvent(ContestId, AtUtc);

public sealed record ScoringConfigChangeAttempt(string ContestId, DateTimeOffset AtUtc) : ContestEvent(ContestId, AtUtc);

public sealed record ActivePersonaSetChangeAttempt(string ContestId, DateTimeOffset AtUtc, IReadOnlyList<string> ProposedPersonaIds) : ContestEvent(ContestId, AtUtc);

public sealed record RuleViolation(string RuleId, string Message);

public interface IContestRule
{
    string RuleId { get; }
    RuleViolation? Evaluate(ContestEvent evt, IContestRuleEvaluationContext context);
}

public interface IContestRuleEvaluationContext
{
    ContestState State { get; }
    bool HasLeadBeenTouchedByOtherPersona(string leadId, string currentPersonaId);
    int OutboundSendCapPerHour { get; }
    IReadOnlyCollection<(int StartHour, int EndHourExclusive)> BlackoutWindowsUtc { get; }
}

public sealed class NoDoubleTouchRule : IContestRule
{
    public string RuleId => "no-double-touch";

    public RuleViolation? Evaluate(ContestEvent evt, IContestRuleEvaluationContext context)
    {
        if (evt is not LeadTouchedEvent lead) return null;
        if (context.HasLeadBeenTouchedByOtherPersona(lead.LeadId, lead.PersonaId))
            return new RuleViolation(RuleId, $"Lead {lead.LeadId} was already touched by another persona in this contest.");
        return null;
    }
}

public sealed class SendRateCapRule : IContestRule
{
    public string RuleId => "send-rate-cap";

    public RuleViolation? Evaluate(ContestEvent evt, IContestRuleEvaluationContext context)
    {
        if (evt is not OutboundSendEvent outbound) return null;
        if (outbound.SendCountInWindow > context.OutboundSendCapPerHour)
            return new RuleViolation(RuleId, $"Send rate {outbound.SendCountInWindow} exceeds cap {context.OutboundSendCapPerHour}.");
        return null;
    }
}

public sealed class BlackoutHoursRule : IContestRule
{
    public string RuleId => "blackout-hours";

    public RuleViolation? Evaluate(ContestEvent evt, IContestRuleEvaluationContext context)
    {
        if (evt is not OutboundSendEvent send) return null;
        var hour = send.AtUtc.UtcDateTime.Hour;
        foreach (var (startHour, endHourEx) in context.BlackoutWindowsUtc)
        {
            if (hour >= startHour && hour < endHourEx)
                return new RuleViolation(RuleId, $"Send at hour {hour:00} falls in blackout window [{startHour:00},{endHourEx:00}).");
        }
        return null;
    }
}

public sealed class ScoringLockedMidContestRule : IContestRule
{
    public string RuleId => "scoring-locked-mid-contest";

    public RuleViolation? Evaluate(ContestEvent evt, IContestRuleEvaluationContext context)
    {
        if (evt is not ScoringConfigChangeAttempt) return null;
        if (context.State.Phase is ContestPhase.Running or ContestPhase.Paused)
            return new RuleViolation(RuleId, "Scoring config is locked while the contest is running or paused.");
        return null;
    }
}

public sealed class PersonaActiveSetLockedRule : IContestRule
{
    public string RuleId => "persona-active-set-locked";

    public RuleViolation? Evaluate(ContestEvent evt, IContestRuleEvaluationContext context)
    {
        if (evt is not ActivePersonaSetChangeAttempt change) return null;
        if (context.State.Phase is ContestPhase.Running or ContestPhase.Paused)
        {
            var current = context.State.ActivePersonaIds;
            if (current.Count != change.ProposedPersonaIds.Count) return Violation();
            for (int i = 0; i < current.Count; i++)
                if (!string.Equals(current[i], change.ProposedPersonaIds[i], StringComparison.Ordinal))
                    return Violation();
        }
        return null;

        RuleViolation Violation() => new(RuleId, "Active persona set is locked while the contest is running or paused.");
    }
}

public sealed class RulesEngine
{
    private readonly IReadOnlyList<IContestRule> _rules;

    public RulesEngine(IReadOnlyList<IContestRule>? rules = null)
    {
        _rules = rules ?? new IContestRule[]
        {
            new NoDoubleTouchRule(),
            new SendRateCapRule(),
            new BlackoutHoursRule(),
            new ScoringLockedMidContestRule(),
            new PersonaActiveSetLockedRule(),
        };
    }

    public IReadOnlyList<IContestRule> Rules => _rules;

    public RuleViolation? Evaluate(ContestEvent evt, IContestRuleEvaluationContext context)
    {
        foreach (var rule in _rules)
        {
            var violation = rule.Evaluate(evt, context);
            if (violation is not null) return violation;
        }
        return null;
    }
}
