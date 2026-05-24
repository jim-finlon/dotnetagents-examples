using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SalesArena.Training.PostContestAnalysis;

public interface IPostContestAnalyzer
{
    Task<PromptVariantSuggestionSet> AnalyzeAsync(
        IContestLedgerReader ledger,
        string contestId,
        CancellationToken ct = default);
}

/// <summary>
/// Deterministic post-contest analyzer. Reads events from the supplied ledger,
/// rolls them up per persona, and emits documented prompt-variant suggestions
/// based on win rate + objection-mix thresholds.
/// </summary>
public sealed class PostContestAnalyzer : IPostContestAnalyzer
{
    public const double LowWinRateThreshold = 0.25;
    public const double HighWinRateThreshold = 0.65;
    public const int MinDecisionsForWinRate = 4;
    public const double PriceObjectionDominanceThreshold = 0.50;

    public Task<PromptVariantSuggestionSet> AnalyzeAsync(
        IContestLedgerReader ledger,
        string contestId,
        CancellationToken ct = default)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        if (string.IsNullOrWhiteSpace(contestId))
            throw new ArgumentException("contestId is required", nameof(contestId));

        var events = ledger.ReadEvents(contestId);
        var diagnostics = new List<string>();
        if (events.Count == 0)
        {
            diagnostics.Add($"Ledger has no events for contest {contestId}.");
            return Task.FromResult(new PromptVariantSuggestionSet(
                contestId, Array.Empty<PromptVariantSuggestion>(), diagnostics));
        }

        var byPersona = RollUp(events);
        var suggestions = new List<PromptVariantSuggestion>();

        foreach (var perf in byPersona.Values.OrderBy(p => p.PersonaId, StringComparer.Ordinal))
        {
            suggestions.Add(SuggestFor(perf));
        }

        return Task.FromResult(new PromptVariantSuggestionSet(contestId, suggestions, diagnostics));
    }

    private static Dictionary<string, PersonaPerformance> RollUp(IReadOnlyList<ContestEventEntry> events)
    {
        var touches = new Dictionary<string, int>(StringComparer.Ordinal);
        var wins = new Dictionary<string, int>(StringComparer.Ordinal);
        var losses = new Dictionary<string, int>(StringComparer.Ordinal);
        var objections = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (var e in events)
        {
            switch (e.EventKind)
            {
                case "lead-touched":
                    touches[e.PersonaId] = touches.GetValueOrDefault(e.PersonaId) + 1;
                    break;
                case "deal-won":
                    wins[e.PersonaId] = wins.GetValueOrDefault(e.PersonaId) + 1;
                    break;
                case "deal-lost":
                    losses[e.PersonaId] = losses.GetValueOrDefault(e.PersonaId) + 1;
                    break;
                case "objection-raised":
                    if (!objections.TryGetValue(e.PersonaId, out var bucket))
                    {
                        bucket = new Dictionary<string, int>(StringComparer.Ordinal);
                        objections[e.PersonaId] = bucket;
                    }
                    var topic = e.ObjectionTopic ?? "unspecified";
                    bucket[topic] = bucket.GetValueOrDefault(topic) + 1;
                    break;
            }
        }

        var allPersonas = new HashSet<string>(touches.Keys, StringComparer.Ordinal);
        allPersonas.UnionWith(wins.Keys);
        allPersonas.UnionWith(losses.Keys);
        allPersonas.UnionWith(objections.Keys);

        var result = new Dictionary<string, PersonaPerformance>(StringComparer.Ordinal);
        foreach (var p in allPersonas)
        {
            result[p] = new PersonaPerformance(
                PersonaId: p,
                Touches: touches.GetValueOrDefault(p),
                Wins: wins.GetValueOrDefault(p),
                Losses: losses.GetValueOrDefault(p),
                ObjectionsByTopic: objections.TryGetValue(p, out var b)
                    ? b
                    : new Dictionary<string, int>(StringComparer.Ordinal));
        }
        return result;
    }

    private static PromptVariantSuggestion SuggestFor(PersonaPerformance perf)
    {
        var decisions = perf.Wins + perf.Losses;
        if (decisions >= MinDecisionsForWinRate && perf.WinRate < LowWinRateThreshold)
        {
            if (PriceObjectionDominates(perf))
                return new PromptVariantSuggestion(
                    perf.PersonaId,
                    "Lead with calibration-value framing in opening 30 seconds.",
                    $"Win rate {perf.WinRate:P0} below {LowWinRateThreshold:P0} and price objections dominate ({SharePrice(perf):P0}).",
                    "lead-with-calibration-value");

            return new PromptVariantSuggestion(
                perf.PersonaId,
                "Tighten qualification gate before pitch.",
                $"Win rate {perf.WinRate:P0} below {LowWinRateThreshold:P0} threshold across {decisions} closes.",
                "tighten-qualification");
        }

        if (perf.TotalObjections > 0 && SharePrice(perf) >= PriceObjectionDominanceThreshold)
            return new PromptVariantSuggestion(
                perf.PersonaId,
                "Open with calibration-value framing to defuse price objections.",
                $"Price objections are {SharePrice(perf):P0} of total objections.",
                "lead-with-calibration-value");

        if (decisions >= MinDecisionsForWinRate && perf.WinRate >= HighWinRateThreshold)
            return new PromptVariantSuggestion(
                perf.PersonaId,
                "Soften tone slightly; replicate this opening on adjacent personas.",
                $"Win rate {perf.WinRate:P0} above {HighWinRateThreshold:P0}; export the pattern.",
                "soften-tone");

        return new PromptVariantSuggestion(
            perf.PersonaId,
            "No change recommended.",
            decisions < MinDecisionsForWinRate
                ? $"Only {decisions} close(s) recorded; below minimum {MinDecisionsForWinRate} for win-rate decisions."
                : "Mid-band win rate; no objection dominance detected.",
            "no-change");
    }

    private static bool PriceObjectionDominates(PersonaPerformance perf) =>
        perf.TotalObjections > 0 && SharePrice(perf) >= PriceObjectionDominanceThreshold;

    private static double SharePrice(PersonaPerformance perf)
    {
        if (perf.TotalObjections == 0) return 0.0;
        var price = perf.ObjectionsByTopic.TryGetValue("price", out var n) ? n : 0;
        return (double)price / perf.TotalObjections;
    }
}
