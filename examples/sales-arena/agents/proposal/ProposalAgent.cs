using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SalesArena.Proposal;

public interface IProposalAgent
{
    Proposal ComposeProposal(ProposalContext context);
}

public sealed class ProposalAgent : IProposalAgent
{
    public Proposal ComposeProposal(ProposalContext context)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (string.IsNullOrWhiteSpace(context.ProspectId))
            throw new ArgumentException("ProspectId is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.PersonaId))
            throw new ArgumentException("PersonaId is required.", nameof(context));
        if (context.StarterMonthlyUsd <= 0 || context.ProMonthlyUsd <= 0 || context.EnterpriseMonthlyUsd <= 0)
            throw new ArgumentException("All tier prices must be > 0.", nameof(context));
        if (context.StarterValueProps is null || context.ProValueProps is null || context.EnterpriseValueProps is null)
            throw new ArgumentException("ValueProps for all tiers must be non-null.", nameof(context));

        var starter = new ProposalPackage(
            Tier: ProposalTier.Starter,
            DisplayName: "Starter",
            MonthlyUsd: context.StarterMonthlyUsd,
            ValueProps: context.StarterValueProps,
            PricingLines: new[] { new PricingLine("starter", "Starter monthly", context.StarterMonthlyUsd) },
            AddOns: Array.Empty<string>());

        var pro = new ProposalPackage(
            Tier: ProposalTier.Pro,
            DisplayName: "Pro",
            MonthlyUsd: context.ProMonthlyUsd,
            ValueProps: context.ProValueProps,
            PricingLines: new[] { new PricingLine("pro", "Pro monthly", context.ProMonthlyUsd) },
            AddOns: Array.Empty<string>());

        var enterprise = new ProposalPackage(
            Tier: ProposalTier.Enterprise,
            DisplayName: "Enterprise",
            MonthlyUsd: context.EnterpriseMonthlyUsd,
            ValueProps: context.EnterpriseValueProps,
            PricingLines: new[] { new PricingLine("enterprise", "Enterprise monthly", context.EnterpriseMonthlyUsd) },
            AddOns: context.EnterpriseAddOns ?? Array.Empty<string>());

        return new Proposal(
            ProspectId: context.ProspectId.Trim(),
            PersonaId: context.PersonaId.Trim(),
            Tiers: new[] { starter, pro, enterprise },
            Citations: context.Citations ?? Array.Empty<string>());
    }
}

public static class ProposalMarkdownExtensions
{
    public static string ToMarkdown(this Proposal proposal)
    {
        if (proposal is null) throw new ArgumentNullException(nameof(proposal));
        var sb = new StringBuilder();
        sb.Append("# Proposal — ").Append(proposal.ProspectId).Append(" / persona: ").AppendLine(proposal.PersonaId);
        sb.AppendLine();

        foreach (var tier in proposal.Tiers.OrderBy(t => t.Tier))
        {
            sb.Append("## ").Append(tier.DisplayName).Append(" — $").Append(tier.MonthlyUsd.ToString("0.00")).AppendLine("/mo");
            sb.AppendLine();

            sb.AppendLine("**Value:**");
            if (tier.ValueProps.Count == 0) sb.AppendLine("- _none specified_");
            else foreach (var v in tier.ValueProps) sb.Append("- **").Append(v.Title).Append(":** ").AppendLine(v.Detail);
            sb.AppendLine();

            sb.AppendLine("**Pricing:**");
            foreach (var pl in tier.PricingLines)
                sb.Append("- `").Append(pl.Sku).Append("` ").Append(pl.Description).Append(" — $").Append(pl.MonthlyUsd.ToString("0.00")).AppendLine();
            sb.AppendLine();

            if (tier.AddOns.Count > 0)
            {
                sb.AppendLine("**Add-ons:**");
                foreach (var a in tier.AddOns) sb.Append("- ").AppendLine(a);
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Citations");
        if (proposal.Citations.Count == 0) sb.AppendLine("_None._");
        else for (int i = 0; i < proposal.Citations.Count; i++)
            sb.Append("[").Append(i + 1).Append("] ").AppendLine(proposal.Citations[i]);

        return sb.ToString();
    }
}
