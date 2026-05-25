using System.Globalization;
using System.Text;

namespace SalesArena.Replay.Counterfactual;

/// <summary>
/// Renders a <see cref="CounterfactualResult"/> as side-by-side Markdown.
/// One table per persona with original / counterfactual / delta columns
/// for every metric. Operators paste the output into the replay report.
/// </summary>
public static class CounterfactualDiffRenderer
{
    public static string RenderMarkdown(CounterfactualResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("# Counterfactual — ").Append(result.Mutation.Kind).Append('\n').Append('\n');
        sb.Append("> Original: `").Append(result.OriginalContestId)
          .Append("`  ·  Counterfactual: `").Append(result.Counterfactual.ContestId).Append("`\n\n");

        sb.Append("## Side-by-side\n\n");
        sb.Append("| Persona | Metric | Original | Counterfactual | Δ |\n");
        sb.Append("| --- | --- | ---: | ---: | ---: |\n");

        foreach (var diff in result.Diff.Personas)
        {
            var o = result.Original.Find(diff.Persona);
            var c = result.Counterfactual.Find(diff.Persona);
            if (o is null || c is null) continue;
            RenderRow(sb, diff.Persona, "Touches", o.TouchesSent, c.TouchesSent, diff.TouchesDelta, inv);
            RenderRow(sb, diff.Persona, "Meetings", o.MeetingsHeld, c.MeetingsHeld, diff.MeetingsDelta, inv);
            RenderRow(sb, diff.Persona, "Deals won", o.DealsWon, c.DealsWon, diff.DealsWonDelta, inv);
            RenderRow(sb, diff.Persona, "Deals lost", o.DealsLost, c.DealsLost, diff.DealsLostDelta, inv);
            RenderRow(sb, diff.Persona, "Revenue $", o.RevenueUsd, c.RevenueUsd, diff.RevenueDeltaUsd, inv);
            RenderRow(sb, diff.Persona, "Final position", o.FinalPosition, c.FinalPosition, diff.PositionDelta, inv);
        }

        sb.Append('\n');
        if (result.Diff.IsZero)
        {
            sb.Append("**No-op:** the mutation produced no behavior change. Every delta is zero.\n");
        }
        return sb.ToString();
    }

    private static void RenderRow<T>(StringBuilder sb, string persona, string metric, T original, T counterfactual, T delta, CultureInfo inv)
        where T : IFormattable
    {
        sb.Append("| ").Append(persona)
          .Append(" | ").Append(metric)
          .Append(" | ").Append(original.ToString(null, inv))
          .Append(" | ").Append(counterfactual.ToString(null, inv))
          .Append(" | ").Append(FormatDelta(delta, inv))
          .Append(" |\n");
    }

    private static string FormatDelta<T>(T value, CultureInfo inv) where T : IFormattable
    {
        var raw = value.ToString(null, inv);
        if (string.IsNullOrEmpty(raw)) return "0";
        if (raw.StartsWith('-')) return raw;
        if (raw == "0" || raw == "0.0") return "0";
        return "+" + raw;
    }
}
