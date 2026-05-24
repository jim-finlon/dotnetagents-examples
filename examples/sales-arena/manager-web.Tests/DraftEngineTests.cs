using SalesArena.Draft;
using SalesArena.Orchestrator.Ledger;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class DraftEngineTests
{
    [Fact]
    public void Elo_seeded_snake_order_reverses_each_round()
    {
        var state = PersonaDraftEngine.Create("contest-1", Operators(), Personas(), picksPerOperator: 3);

        Assert.Equal(
            ["avery", "blake", "casey", "casey", "blake", "avery", "avery", "blake", "casey"],
            state.PickOrder);
    }

    [Fact]
    public void Pick_pins_persona_to_operator_pod_slot_and_emits_ledger_event()
    {
        var state = PersonaDraftEngine.Create("contest-1", Operators(), Personas(), picksPerOperator: 3);

        var result = PersonaDraftEngine.Pick(
            state,
            "avery",
            "roma",
            expectedRevision: 0,
            DateTimeOffset.Parse("2026-05-18T12:30:00Z"));

        var assignment = Assert.Single(result.State.PodAssignments);
        Assert.Equal("avery", assignment.OperatorId);
        Assert.Equal(1, assignment.PodSlot);
        Assert.Equal("roma", assignment.PersonaId);
        Assert.Equal(ArenaEventKinds.DraftPickMade, result.Event.Kind);

        var payload = result.Event.GetPayload<DraftPickMadePayload>()!;
        Assert.Equal("contest-1", payload.ContestId);
        Assert.Equal("avery", payload.OperatorId);
        Assert.Equal("roma", payload.PersonaId);
        Assert.Equal(1, payload.PickNumber);
        Assert.Equal(1, payload.PodSlot);
        Assert.Equal(1, payload.DraftRevision);
    }

    [Fact]
    public void Pick_refuses_stale_revision_for_optimistic_concurrency()
    {
        var state = PersonaDraftEngine.Create("contest-1", Operators(), Personas(), picksPerOperator: 3);
        var result = PersonaDraftEngine.Pick(state, "avery", "roma", 0, DateTimeOffset.UtcNow);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            PersonaDraftEngine.Pick(result.State, "blake", "moss", 0, DateTimeOffset.UtcNow));

        Assert.Contains("changed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<DraftOperator> Operators() =>
    [
        new("casey", "Casey", null),
        new("avery", "Avery", 1700),
        new("blake", "Blake", 1600),
    ];

    private static IReadOnlyList<DraftPersona> Personas() =>
    [
        new("roma", "Roma"),
        new("moss", "Moss"),
        new("aaronow", "Aaronow"),
        new("levene", "Levene"),
        new("williamson", "Williamson"),
        new("engineer", "Engineer", IsCommunityUpload: true),
        new("hardballer", "Hardballer", IsCommunityUpload: true),
        new("influencer", "Influencer", IsCommunityUpload: true),
        new("the-silent-one", "The Silent One", IsCommunityUpload: true),
    ];
}
