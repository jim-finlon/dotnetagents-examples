using System.Collections.Generic;

namespace SalesArena.Proposal;

public enum ProposalTier
{
    Starter = 0,
    Pro = 1,
    Enterprise = 2,
}

public sealed record ValueProp(string Title, string Detail);

public sealed record PricingLine(string Sku, string Description, decimal MonthlyUsd);

public sealed record ProposalPackage(
    ProposalTier Tier,
    string DisplayName,
    decimal MonthlyUsd,
    IReadOnlyList<ValueProp> ValueProps,
    IReadOnlyList<PricingLine> PricingLines,
    IReadOnlyList<string> AddOns);

public sealed record ProposalContext(
    string ProspectId,
    string PersonaId,
    decimal StarterMonthlyUsd,
    decimal ProMonthlyUsd,
    decimal EnterpriseMonthlyUsd,
    IReadOnlyList<ValueProp> StarterValueProps,
    IReadOnlyList<ValueProp> ProValueProps,
    IReadOnlyList<ValueProp> EnterpriseValueProps,
    IReadOnlyList<string> EnterpriseAddOns,
    IReadOnlyList<string> Citations);

public sealed record Proposal(
    string ProspectId,
    string PersonaId,
    IReadOnlyList<ProposalPackage> Tiers,
    IReadOnlyList<string> Citations);

public sealed record ProposalProspect(
    string Company,
    string FirstName,
    string Role,
    string Timezone,
    string RegulatedIndustryOrEuOrCustomResidency);

