using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SalesArena.Replay.Counterfactual;

/// <summary>
/// Reference simulator used by tests + the counterfactual runner when no
/// production simulator is registered. Per-persona outcomes are derived
/// from a stable SHA-256 hash of (persona, configs, seed, leadPoolSize)
/// so identical inputs always yield identical outputs (the deterministic-
/// re-run invariant), and any field change produces a different outcome.
///
/// <para>The values are not pretending to model real sales dynamics —
/// they're a stand-in for the production simulator that lets the
/// counterfactual diff pipeline (renderer, no-op-zero-delta, mutation
/// math) be verified end-to-end without SA-02-05 contest-lifecycle
/// shipping first.</para>
/// </summary>
public sealed class DeterministicHashSimulator : IContestSimulator
{
    public ContestOutcome Simulate(string contestId, IReadOnlyList<PersonaConfig> personas, int leadPoolSize, int seed)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentException.ThrowIfNullOrEmpty(contestId);

        var raw = personas.Select(p => SimulateSingle(p, leadPoolSize, seed)).ToArray();

        // Final position is ordering by revenue desc, ordinal-asc tiebreak.
        var ranked = raw
            .OrderByDescending(p => p.RevenueUsd)
            .ThenBy(p => p.Persona, StringComparer.Ordinal)
            .Select((p, idx) => p with { FinalPosition = idx + 1 })
            .ToArray();

        // Re-order back to original persona order so callers can index by
        // input position.
        var byPersona = ranked.ToDictionary(p => p.Persona, StringComparer.Ordinal);
        var ordered = personas.Select(p => byPersona[p.Persona]).ToArray();

        return new ContestOutcome(contestId, ordered);
    }

    private static PersonaOutcome SimulateSingle(PersonaConfig config, int leadPoolSize, int seed)
    {
        // Stable hash → 8 bytes of derivable per-persona figures.
        var key = string.Create(CultureInfo.InvariantCulture,
            $"{config.Persona}|{config.OutreachTemplatesRef}|{config.ModelTier}|{config.CadenceRef}|{leadPoolSize}|{seed}");
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(key), hash);

        // Map hash bytes → bounded figures. The ranges are tuned to land
        // within plausible contest output magnitudes for a typical
        // demo-mode lead pool size, but the numbers are entirely
        // deterministic — no real-world signal.
        var touches = 10 + (hash[0] % 80);              // 10..89 touches
        var meetings = 1 + (hash[1] % 12);              // 1..12 meetings
        var dealsWon = hash[2] % 8;                     // 0..7 wins
        var dealsLost = hash[3] % 6;                    // 0..5 losses
        var revenue = (decimal)((hash[4] << 8 | hash[5]) % 100_000); // up to $99,999

        return new PersonaOutcome(
            Persona: config.Persona,
            TouchesSent: touches,
            MeetingsHeld: meetings,
            DealsWon: dealsWon,
            DealsLost: dealsLost,
            RevenueUsd: revenue,
            FinalPosition: 0); // assigned by caller after ranking
    }
}
