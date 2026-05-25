using System.Globalization;
using System.Text;

namespace SalesArena.BakeOff;

/// <summary>
/// Markdown report renderer for a <see cref="BakeOffResult"/>. Includes a
/// loud disclaimer header (per story RiskNote: "Loud disclaimer in the
/// report header") and a confidential-data badge when applicable.
/// </summary>
public static class BakeOffReportRenderer
{
    public const string Disclaimer =
        "**Simulation-only.** This bake-off compared two product profiles using simulated personas + synthetic prospects. It is not a replacement for a real evaluation: prospect intent, market timing, and team-specific skill all matter outside this sandbox. Use the verdict as one signal, not the deciding one.";

    public static string RenderMarkdown(BakeOffResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.Append("# Vendor Bake-Off — ")
          .Append(result.ProductA.Name).Append(" vs ").Append(result.ProductB.Name).Append('\n').Append('\n');
        sb.Append("> ").Append(Disclaimer).Append('\n').Append('\n');

        if (result.ContainsConfidentialData)
        {
            sb.Append("> ⚠ **Confidential data:** one or both product profiles were marked as containing customer-confidential data. Do not share this report outside the operator's organization.\n\n");
        }

        sb.Append("**Seed:** `").Append(result.Seed).Append("`  ·  **Lead pool:** ").Append(result.LeadPoolSize)
          .Append("  ·  **Completed:** ").Append(result.CompletedAtUtc.UtcDateTime.ToString("u", inv))
          .Append("\n\n");

        sb.Append("## Per-persona\n\n");
        sb.Append("| Persona | ").Append(result.ProductA.Name).Append(" rev $ | ")
          .Append(result.ProductB.Name).Append(" rev $ | Δ ($) | Preferred |\n");
        sb.Append("| --- | ---: | ---: | ---: | --- |\n");
        foreach (var p in result.PerPersona)
        {
            sb.Append("| ").Append(p.Persona)
              .Append(" | ").Append(p.WithProductA.RevenueUsd.ToString(inv))
              .Append(" | ").Append(p.WithProductB.RevenueUsd.ToString(inv))
              .Append(" | ").Append(FormatSignedDecimal(p.RevenueDeltaUsd, inv))
              .Append(" | ").Append(p.PreferredProduct ?? "tied")
              .Append(" |\n");
        }

        sb.Append('\n').Append("## Aggregate\n\n");
        sb.Append("- Personas preferring **").Append(result.ProductA.Name).Append("**: ")
          .Append(result.Aggregate.PersonasPreferringA).Append('\n');
        sb.Append("- Personas preferring **").Append(result.ProductB.Name).Append("**: ")
          .Append(result.Aggregate.PersonasPreferringB).Append('\n');
        sb.Append("- Tied: ").Append(result.Aggregate.PersonasTied).Append('\n');
        sb.Append("- Total revenue delta (A − B): ")
          .Append(FormatSignedDecimal(result.Aggregate.TotalRevenueDeltaUsd, inv)).Append('\n');
        sb.Append("- **Overall verdict:** ")
          .Append(result.Aggregate.OverallVerdict ?? "tied at the aggregate level — both products performed comparably across this persona mix").Append('\n');

        return sb.ToString();
    }

    private static string FormatSignedDecimal(decimal value, CultureInfo inv)
    {
        var raw = value.ToString(inv);
        if (raw.StartsWith('-')) return raw;
        if (value == 0m) return "0";
        return "+" + raw;
    }
}
