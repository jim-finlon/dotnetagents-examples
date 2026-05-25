namespace SalesArena.BakeOff;

/// <summary>
/// Operator-supplied vendor profile. Two of these enter a bake-off side
/// by side. Fields are intentionally minimal so an operator can hand-edit
/// a yaml file in five minutes; richer features are encoded in
/// <see cref="Features"/> as free-text tags.
/// </summary>
public sealed record ProductProfile(
    string Name,
    string PricingTier,
    IReadOnlyList<string> Features,
    string IdealCustomerProfile,
    bool ContainsConfidentialData = false);
