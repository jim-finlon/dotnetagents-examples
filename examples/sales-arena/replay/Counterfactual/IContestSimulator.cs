namespace SalesArena.Replay.Counterfactual;

/// <summary>
/// Pure-function deterministic simulator. Given the same persona configs,
/// same seed, and same lead-pool shape, MUST return the same outcome.
/// Production hosts plug a real contest-lifecycle (SA-02-05) impl; tests
/// + offline replay use <see cref="DeterministicHashSimulator"/>.
/// </summary>
public interface IContestSimulator
{
    ContestOutcome Simulate(string contestId, IReadOnlyList<PersonaConfig> personas, int leadPoolSize, int seed);
}
