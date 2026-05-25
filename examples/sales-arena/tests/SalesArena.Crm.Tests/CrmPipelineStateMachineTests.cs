using FluentAssertions;
using SalesArena.Crm;
using Xunit;

namespace SalesArena.Crm.Tests;

/// <summary>
/// Pins the legal lifecycle, illegal-transition rejection, and event
/// emission. The state machine is the foundation that SA-01-02, SA-01-08
/// + SA-02-03 all sit on, so this suite is the hardest gate on SA-01-01.
/// </summary>
public sealed class CrmPipelineStateMachineTests
{
    [Fact]
    public async Task Advance_legal_lead_to_researched_publishes_event_and_logs()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());

        CrmStageChangedEvent? captured = null;
        publisher.StageChanged += (_, e) => captured = e;

        var lead = NewLead("L-0001", CrmStages.Lead, "roma");
        var evt = await pipeline.AdvanceAsync(lead, CrmStages.Researched, evidenceRef: "research-brief-1");

        lead.Stage.Should().Be(CrmStages.Researched);
        evt.FromStage.Should().Be(CrmStages.Lead);
        evt.ToStage.Should().Be(CrmStages.Researched);
        evt.LeadId.Should().Be("L-0001");
        evt.Persona.Should().Be("roma");
        evt.EvidenceRef.Should().Be("research-brief-1");

        captured.Should().NotBeNull();
        captured!.LeadId.Should().Be("L-0001");

        var entries = await log.GetByLeadAsync("L-0001");
        entries.Should().HaveCount(1);
        entries[0].FromStage.Should().Be(CrmStages.Lead);
        entries[0].ToStage.Should().Be(CrmStages.Researched);
    }

    [Fact]
    public async Task Advance_walks_the_full_canonical_pipeline_to_closed_won()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());

        var lead = NewLead("L-0001", CrmStages.Lead, "moss");
        await pipeline.AdvanceAsync(lead, CrmStages.Researched);
        await pipeline.AdvanceAsync(lead, CrmStages.Contacted);
        await pipeline.AdvanceAsync(lead, CrmStages.Qualified);
        await pipeline.AdvanceAsync(lead, CrmStages.DemoBooked);
        await pipeline.AdvanceAsync(lead, CrmStages.DemoHeld);
        await pipeline.AdvanceAsync(lead, CrmStages.ProposalSent);
        await pipeline.AdvanceAsync(lead, CrmStages.Negotiating);
        await pipeline.AdvanceAsync(lead, CrmStages.ClosedWon);

        lead.Stage.Should().Be(CrmStages.ClosedWon);
        var entries = await log.GetByLeadAsync("L-0001");
        entries.Should().HaveCount(8);
        entries.Select(e => e.ToStage).Should().Equal(new[]
        {
            CrmStages.Researched,
            CrmStages.Contacted,
            CrmStages.Qualified,
            CrmStages.DemoBooked,
            CrmStages.DemoHeld,
            CrmStages.ProposalSent,
            CrmStages.Negotiating,
            CrmStages.ClosedWon,
        });
    }

    [Fact]
    public async Task Advance_rejects_illegal_skip_from_lead_to_proposal_sent()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());
        var lead = NewLead("L-0001", CrmStages.Lead, "roma");

        var act = async () => await pipeline.AdvanceAsync(lead, CrmStages.ProposalSent);

        var ex = await act.Should().ThrowAsync<CrmStateException>();
        ex.Which.Code.Should().Be(CrmStateException.Codes.IllegalTransition);
        // Record + log unchanged on illegal transition
        lead.Stage.Should().Be(CrmStages.Lead);
        (await log.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Advance_refuses_target_with_unknown_stage_name()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());
        var lead = NewLead("L-0001", CrmStages.Lead, "roma");

        var act = async () => await pipeline.AdvanceAsync(lead, "MagicSauce");

        var ex = await act.Should().ThrowAsync<CrmStateException>();
        ex.Which.Code.Should().Be(CrmStateException.Codes.UnknownStage);
    }

    [Fact]
    public async Task Advance_refuses_to_move_a_terminal_record()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());
        var lead = NewLead("L-0001", CrmStages.ClosedWon, "roma");

        var act = async () => await pipeline.AdvanceAsync(lead, CrmStages.Negotiating);

        var ex = await act.Should().ThrowAsync<CrmStateException>();
        ex.Which.Code.Should().Be(CrmStateException.Codes.TerminalStage);
    }

    [Fact]
    public async Task Advance_supports_nurture_reengagement_back_to_contacted()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());

        var lead = NewLead("L-0001", CrmStages.Lead, "aaronow");
        await pipeline.AdvanceAsync(lead, CrmStages.Researched);
        await pipeline.AdvanceAsync(lead, CrmStages.Contacted);
        await pipeline.AdvanceAsync(lead, CrmStages.Nurture); // drift to nurture
        await pipeline.AdvanceAsync(lead, CrmStages.Contacted); // re-engagement signal

        lead.Stage.Should().Be(CrmStages.Contacted);
        var entries = await log.GetByLeadAsync("L-0001");
        entries.Should().HaveCount(4);
        entries[^1].FromStage.Should().Be(CrmStages.Nurture);
        entries[^1].ToStage.Should().Be(CrmStages.Contacted);
    }

    [Fact]
    public void CanTransition_returns_true_for_legal_edge_and_false_for_illegal_edge()
    {
        var publisher = new InMemoryCrmEventPublisher();
        var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());

        var lead = NewLead("L-0001", CrmStages.Qualified, "roma");

        pipeline.CanTransition(lead, CrmStages.DemoBooked).Should().BeTrue();
        pipeline.CanTransition(lead, CrmStages.Nurture).Should().BeTrue();
        pipeline.CanTransition(lead, CrmStages.ClosedLost).Should().BeTrue();

        pipeline.CanTransition(lead, CrmStages.ProposalSent).Should().BeFalse();
        pipeline.CanTransition(lead, CrmStages.Lead).Should().BeFalse();
    }

    [Fact]
    public void GetAvailableNextStages_returns_canonical_set_per_current_stage()
    {
        var publisher = new InMemoryCrmEventPublisher();
        var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());

        var lead = NewLead("L-0001", CrmStages.ProposalSent, "moss");
        var next = pipeline.GetAvailableNextStages(lead).ToHashSet(StringComparer.OrdinalIgnoreCase);

        next.Should().BeEquivalentTo(new[]
        {
            CrmStages.Negotiating,
            CrmStages.ClosedWon,
            CrmStages.ClosedLost,
            CrmStages.Nurture,
        });
    }

    [Fact]
    public void LegalTransitions_table_contains_every_stage_that_appears_in_an_edge()
    {
        // Coverage check: every legal-transition endpoint must be a known stage.
        var allStages = CrmStages.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (from, to) in CrmPipelineStateMachine.LegalTransitions)
        {
            allStages.Should().Contain(from, $"transition from-stage '{from}' must be a known stage");
            allStages.Should().Contain(to, $"transition to-stage '{to}' must be a known stage");
        }

        // Active stages must have at least one outgoing transition; terminals (Won/Lost) must have zero.
        foreach (var stage in CrmStages.All)
        {
            var outgoing = CrmPipelineStateMachine.LegalTransitions.Count(t => t.From == stage);
            if (CrmStages.IsTerminal(stage))
            {
                outgoing.Should().Be(0, $"terminal stage '{stage}' must have no outgoing transitions");
            }
            else
            {
                outgoing.Should().BeGreaterThan(0, $"active stage '{stage}' must have at least one outgoing transition");
            }
        }
    }

    [Fact]
    public async Task Advance_records_evidence_ref_on_log_and_event()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var pipeline = new CrmPipelineStateMachine(publisher, log, FixedTime());

        var lead = NewLead("L-0001", CrmStages.Lead, "levene");
        var evt = await pipeline.AdvanceAsync(lead, CrmStages.Researched, evidenceRef: "intel-bundle-99");

        evt.EvidenceRef.Should().Be("intel-bundle-99");
        (await log.GetByLeadAsync("L-0001"))[0].EvidenceRef.Should().Be("intel-bundle-99");
    }

    [Fact]
    public async Task Advance_sets_updated_at_to_provided_time_provider_now()
    {
        var publisher = new InMemoryCrmEventPublisher();
        await using var log = NewLog();
        var fixedAt = new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);
        var pipeline = new CrmPipelineStateMachine(publisher, log, new FakeTimeProvider(fixedAt));

        var lead = NewLead("L-0001", CrmStages.Lead, "roma");
        var evt = await pipeline.AdvanceAsync(lead, CrmStages.Researched);

        lead.UpdatedAtUtc.Should().Be(fixedAt);
        evt.OccurredAtUtc.Should().Be(fixedAt);
    }

    // --- helpers -----------------------------------------------------------

    private static CrmRecord NewLead(string id, string stage, string persona) => new()
    {
        LeadId = id,
        Stage = stage,
        Persona = persona,
        CreatedAtUtc = new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero),
    };

    private static SqliteActivityLog NewLog() => new("Data Source=:memory:");

    private static TimeProvider FixedTime() => new FakeTimeProvider(new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero));

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
