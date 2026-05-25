using System.Collections.Generic;

namespace SalesArena.Training.PostContestAnalysis;

public sealed record ContestEventEntry(
    string ContestId,
    string PersonaId,
    string EventKind,        // "lead-touched", "objection-raised", "deal-won", "deal-lost"
    string? ObjectionTopic = null);

public sealed record PersonaPerformance(
    string PersonaId,
    int Touches,
    int Wins,
    int Losses,
    IReadOnlyDictionary<string, int> ObjectionsByTopic)
{
    public double WinRate => Wins + Losses == 0 ? 0.0 : (double)Wins / (Wins + Losses);
    public int TotalObjections
    {
        get
        {
            int sum = 0;
            foreach (var v in ObjectionsByTopic.Values) sum += v;
            return sum;
        }
    }
}

public sealed record PromptVariantSuggestion(
    string PersonaId,
    string Suggestion,
    string Reason,
    string SuggestionKind);  // "tighten-qualification", "lead-with-calibration-value", "soften-tone", "no-change"

public sealed record PromptVariantSuggestionSet(
    string ContestId,
    IReadOnlyList<PromptVariantSuggestion> Suggestions,
    IReadOnlyList<string> Diagnostics);
