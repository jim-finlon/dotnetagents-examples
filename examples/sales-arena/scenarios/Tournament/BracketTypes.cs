namespace SalesArena.Tournament;

/// <summary>
/// One slot in a tournament round. Carries the persona id (or null for a
/// bye), the seed assigned at bracket creation, and the result once the
/// round runs.
/// </summary>
public sealed record BracketSlot(
    int SlotIndex,
    string? Persona,
    int Seed,
    bool IsBye)
{
    public static BracketSlot Bye(int slotIndex) => new(slotIndex, Persona: null, Seed: int.MaxValue, IsBye: true);
}

/// <summary>
/// One match in a round. Position is the round-local index; <see cref="A"/>
/// and <see cref="B"/> are the two seeded slots; <see cref="Winner"/> is set
/// when the round runs (or pre-set for byes).
/// </summary>
public sealed record BracketMatch(
    int Position,
    BracketSlot A,
    BracketSlot B,
    string? Winner,
    DateTimeOffset? CompletedAtUtc);

public sealed record BracketRound(
    int RoundNumber,
    IReadOnlyList<BracketMatch> Matches);

public enum BracketStatus
{
    Pending = 1,
    InProgress = 2,
    Completed = 3,
}

public sealed record Bracket(
    string Id,
    IReadOnlyList<string> Personas,
    int LeadsPerRound,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<BracketRound> Rounds,
    BracketStatus Status,
    string? Champion);
