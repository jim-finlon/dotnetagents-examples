using DotNetAgents.Agents.StateMachines;

namespace SalesArena.Crm;

/// <summary>
/// The CRM pipeline state machine. Validates transitions, persists every
/// move to the <see cref="IActivityLog"/>, and publishes
/// <see cref="CrmStageChangedEvent"/>s for downstream subscribers.
///
/// <para>One singleton instance manages the whole population of leads — the
/// per-record stage lives on <see cref="CrmRecord.Stage"/>. The wrapped
/// <see cref="AgentStateMachine{TState}"/> serves as the transition graph
/// (pure-graph + guards), not as a per-record FSM instance.</para>
/// </summary>
public sealed class CrmPipelineStateMachine
{
    private readonly AgentStateMachine<CrmRecord> _graph;
    private readonly ICrmEventPublisher _publisher;
    private readonly IActivityLog _log;
    private readonly TimeProvider _time;

    public CrmPipelineStateMachine(ICrmEventPublisher publisher, IActivityLog log, TimeProvider? time = null)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _time = time ?? TimeProvider.System;
        _graph = BuildGraph();
    }

    /// <summary>
    /// All legal stage transitions in the canonical pipeline.
    /// Public so tests + tooling can introspect the graph without reflection.
    /// </summary>
    public static IReadOnlyList<(string From, string To)> LegalTransitions { get; } = new (string, string)[]
    {
        (CrmStages.Lead, CrmStages.Researched),
        (CrmStages.Lead, CrmStages.Nurture),
        (CrmStages.Researched, CrmStages.Contacted),
        (CrmStages.Researched, CrmStages.Nurture),
        (CrmStages.Contacted, CrmStages.Qualified),
        (CrmStages.Contacted, CrmStages.Nurture),
        (CrmStages.Contacted, CrmStages.ClosedLost),
        (CrmStages.Qualified, CrmStages.DemoBooked),
        (CrmStages.Qualified, CrmStages.Nurture),
        (CrmStages.Qualified, CrmStages.ClosedLost),
        (CrmStages.DemoBooked, CrmStages.DemoHeld),
        (CrmStages.DemoBooked, CrmStages.Nurture),
        (CrmStages.DemoBooked, CrmStages.ClosedLost),
        (CrmStages.DemoHeld, CrmStages.ProposalSent),
        (CrmStages.DemoHeld, CrmStages.Nurture),
        (CrmStages.DemoHeld, CrmStages.ClosedLost),
        (CrmStages.ProposalSent, CrmStages.Negotiating),
        (CrmStages.ProposalSent, CrmStages.ClosedWon),
        (CrmStages.ProposalSent, CrmStages.ClosedLost),
        (CrmStages.ProposalSent, CrmStages.Nurture),
        (CrmStages.Negotiating, CrmStages.ClosedWon),
        (CrmStages.Negotiating, CrmStages.ClosedLost),
        (CrmStages.Negotiating, CrmStages.Nurture),
        // Re-engagement: Nurture is a "soft terminal" — a re-signaled prospect can re-enter the funnel.
        (CrmStages.Nurture, CrmStages.Contacted),
        (CrmStages.Nurture, CrmStages.Researched),
    };

    /// <summary>
    /// Returns true if the supplied transition is legal for the record's current stage.
    /// Pure read — does not mutate the record or the log.
    /// </summary>
    public bool CanTransition(CrmRecord record, string toStage)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(toStage);
        return _graph.CanTransition(record.Stage, toStage, record);
    }

    /// <summary>
    /// Returns every legal next stage for the record. Useful for the
    /// Next-Best-Action engine (SA-01-02) when it asks "what could happen next?".
    /// </summary>
    public IEnumerable<string> GetAvailableNextStages(CrmRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return LegalTransitions
            .Where(t => t.From.Equals(record.Stage, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.To);
    }

    /// <summary>
    /// Apply a stage transition. Mutates <paramref name="record"/>, persists
    /// the activity-log entry, and publishes <see cref="CrmStageChangedEvent"/>.
    /// </summary>
    /// <param name="record">The lead record. Its <c>Stage</c> + <c>UpdatedAtUtc</c> are mutated on success.</param>
    /// <param name="toStage">Target stage. Must satisfy <see cref="CanTransition"/>.</param>
    /// <param name="evidenceRef">Optional evidence pointer (touch id, meeting id, proposal id).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="CrmStateException">If the target stage is unknown or the transition is illegal.</exception>
    public async Task<CrmStageChangedEvent> AdvanceAsync(
        CrmRecord record,
        string toStage,
        string? evidenceRef = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(toStage);

        if (!CrmStages.All.Contains(toStage, StringComparer.OrdinalIgnoreCase))
        {
            throw new CrmStateException(
                $"Unknown CRM stage: '{toStage}'.",
                CrmStateException.Codes.UnknownStage);
        }

        if (CrmStages.IsTerminal(record.Stage))
        {
            throw new CrmStateException(
                $"Lead '{record.LeadId}' is in terminal stage '{record.Stage}'; no further transitions are allowed.",
                CrmStateException.Codes.TerminalStage);
        }

        if (!_graph.CanTransition(record.Stage, toStage, record))
        {
            throw new CrmStateException(
                $"Illegal transition for lead '{record.LeadId}': '{record.Stage}' -> '{toStage}'.",
                CrmStateException.Codes.IllegalTransition);
        }

        var fromStage = record.Stage;
        var occurredAt = _time.GetUtcNow();

        record.Stage = toStage;
        record.UpdatedAtUtc = occurredAt;

        await _log.AppendAsync(
            new ActivityLogEntry(
                Id: 0,
                LeadId: record.LeadId,
                FromStage: fromStage,
                ToStage: toStage,
                Persona: record.Persona,
                OccurredAtUtc: occurredAt,
                EvidenceRef: evidenceRef),
            cancellationToken).ConfigureAwait(false);

        var evt = new CrmStageChangedEvent(
            LeadId: record.LeadId,
            FromStage: fromStage,
            ToStage: toStage,
            Persona: record.Persona,
            OccurredAtUtc: occurredAt,
            EvidenceRef: evidenceRef);

        await _publisher.PublishAsync(evt, cancellationToken).ConfigureAwait(false);
        return evt;
    }

    private static AgentStateMachine<CrmRecord> BuildGraph()
    {
        var sm = new AgentStateMachine<CrmRecord>();
        foreach (var stage in CrmStages.All)
        {
            sm.AddState(stage);
        }
        foreach (var (from, to) in LegalTransitions)
        {
            sm.AddTransition(from, to);
        }
        return sm;
    }
}
