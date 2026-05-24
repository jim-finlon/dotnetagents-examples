using SalesArena.Replay.Counterfactual;

namespace SalesArena.BakeOff;

/// <summary>
/// Runs the same lead pool twice with one persona per product profile,
/// then surfaces the side-by-side comparison + an aggregate verdict.
/// Reuses <see cref="IContestSimulator"/> from SA-08-06 so the determinism
/// invariant is shared with the counterfactual engine.
/// </summary>
public interface IVendorBakeOffEngine
{
    BakeOffResult Run(
        ProductProfile productA,
        ProductProfile productB,
        IReadOnlyList<PersonaConfig> personas,
        int leadPoolSize,
        int seed,
        DateTimeOffset? completedAtUtc = null);
}
