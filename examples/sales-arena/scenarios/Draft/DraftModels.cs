using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Draft;

public sealed record DraftOperator(string OperatorId, string DisplayName, int? PreviousSeasonElo);

public sealed record DraftPersona(string PersonaId, string DisplayName, bool IsCommunityUpload = false);

public sealed record DraftPick(
    string OperatorId,
    string PersonaId,
    int Round,
    int PickNumber,
    int PodSlot);

public sealed record DraftPodAssignment(string OperatorId, int PodSlot, string PersonaId);

public sealed record DraftPickMadePayload(
    string ContestId,
    string OperatorId,
    string PersonaId,
    int Round,
    int PickNumber,
    int PodSlot,
    int DraftRevision);

public sealed record DraftPickResult(DraftState State, ArenaEvent Event);

public sealed record DraftState
{
    public required string ContestId { get; init; }
    public required IReadOnlyList<DraftOperator> Operators { get; init; }
    public required IReadOnlyList<DraftPersona> FreeAgents { get; init; }
    public required IReadOnlyList<string> PickOrder { get; init; }
    public required IReadOnlyList<DraftPick> Picks { get; init; }
    public required IReadOnlyList<DraftPodAssignment> PodAssignments { get; init; }
    public required int PicksPerOperator { get; init; }
    public required int Revision { get; init; }

    public bool IsComplete => Picks.Count >= Operators.Count * PicksPerOperator;
    public IReadOnlyList<string> AvailablePersonaIds => FreeAgents
        .Select(persona => persona.PersonaId)
        .Except(Picks.Select(pick => pick.PersonaId), StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public string? CurrentOperatorId =>
        IsComplete ? null : PickOrder[Picks.Count];
}

public static class PersonaDraftEngine
{
    public static DraftState Create(
        string contestId,
        IReadOnlyList<DraftOperator> operators,
        IReadOnlyList<DraftPersona> freeAgents,
        int picksPerOperator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contestId);

        if (operators.Count < 2)
            throw new InvalidOperationException("Persona drafts require a multi-operator contest.");

        if (picksPerOperator is < 3 or > 5)
            throw new ArgumentOutOfRangeException(nameof(picksPerOperator), picksPerOperator, "Operators must draft 3 to 5 personas.");

        if (freeAgents.Count < operators.Count * picksPerOperator)
            throw new InvalidOperationException("The free-agent pool does not contain enough personas for the requested draft.");

        var seededOperators = operators
            .OrderByDescending(op => op.PreviousSeasonElo ?? 0)
            .ThenBy(op => op.OperatorId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DraftState
        {
            ContestId = contestId,
            Operators = seededOperators,
            FreeAgents = freeAgents
                .OrderBy(persona => persona.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            PickOrder = BuildSnakeOrder(seededOperators, picksPerOperator),
            Picks = Array.Empty<DraftPick>(),
            PodAssignments = Array.Empty<DraftPodAssignment>(),
            PicksPerOperator = picksPerOperator,
            Revision = 0,
        };
    }

    public static DraftPickResult Pick(
        DraftState state,
        string operatorId,
        string personaId,
        int expectedRevision,
        DateTimeOffset occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(operatorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);

        if (state.Revision != expectedRevision)
            throw new InvalidOperationException("Draft state changed before this pick was submitted.");

        if (state.IsComplete)
            throw new InvalidOperationException("Draft is already complete.");

        if (!string.Equals(state.CurrentOperatorId, operatorId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"It is {state.CurrentOperatorId}'s pick.");

        if (!state.AvailablePersonaIds.Contains(personaId, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Persona is not available in the free-agent pool.");

        var pickNumber = state.Picks.Count + 1;
        var round = ((pickNumber - 1) / state.Operators.Count) + 1;
        var podSlot = state.Picks.Count(pick => pick.OperatorId.Equals(operatorId, StringComparison.OrdinalIgnoreCase)) + 1;
        var pick = new DraftPick(operatorId, personaId, round, pickNumber, podSlot);
        var nextRevision = state.Revision + 1;

        var nextState = state with
        {
            Picks = state.Picks.Concat([pick]).ToArray(),
            PodAssignments = state.PodAssignments
                .Concat([new DraftPodAssignment(operatorId, podSlot, personaId)])
                .ToArray(),
            Revision = nextRevision,
        };

        var payload = new DraftPickMadePayload(
            state.ContestId,
            operatorId,
            personaId,
            round,
            pickNumber,
            podSlot,
            nextRevision);

        var arenaEvent = new ArenaEvent
        {
            ContestId = state.ContestId,
            Kind = ArenaEventKinds.DraftPickMade,
            OccurredAtUtc = occurredAtUtc,
            Persona = personaId,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        };

        return new DraftPickResult(nextState, arenaEvent);
    }

    private static IReadOnlyList<string> BuildSnakeOrder(IReadOnlyList<DraftOperator> seededOperators, int picksPerOperator)
    {
        var order = new List<string>(seededOperators.Count * picksPerOperator);
        for (var round = 0; round < picksPerOperator; round++)
        {
            var roundOperators = round % 2 == 0
                ? seededOperators
                : seededOperators.Reverse();

            order.AddRange(roundOperators.Select(op => op.OperatorId));
        }

        return order;
    }
}
