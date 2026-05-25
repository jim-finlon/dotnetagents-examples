namespace SalesArena.Replay.Counterfactual;

public sealed record CounterfactualRunOptions(
    int LeadPoolSize,
    int Seed,
    string CounterfactualContestIdSuffix = "-cf");

public interface ICounterfactualRunner
{
    /// <summary>
    /// Run a contest twice — once with the original persona configs, once
    /// with <paramref name="mutation"/> applied — and return the
    /// side-by-side outcome + diff. The same simulator + same seed +
    /// same lead pool must be used for both runs (deterministic-re-run
    /// invariant pinned by tests).
    /// </summary>
    CounterfactualResult Run(
        string originalContestId,
        IReadOnlyList<PersonaConfig> originalPersonas,
        CounterfactualMutation mutation,
        CounterfactualRunOptions options);
}
