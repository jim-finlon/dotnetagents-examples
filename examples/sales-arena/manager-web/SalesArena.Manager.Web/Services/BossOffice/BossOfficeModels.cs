namespace SalesArena.Manager.Web.Services.BossOffice;

public sealed record BossOfficeCostCatalog(
    DateTimeOffset AsOfUtc,
    decimal CostPerTouchUsd,
    IReadOnlyList<ModelTierSpend> ModelTierSpend);

public sealed record ModelTierSpend(string TierName, decimal SpendUsd);

public sealed record BossOfficeMetricsSnapshot(
    DateTimeOffset AsOfUtc,
    DateTimeOffset GeneratedAtUtc,
    decimal ContestRoiUsd,
    decimal CostPerTouchUsd,
    IReadOnlyList<CostPerTouchPoint> CostPerTouchTrend,
    IReadOnlyList<ModelTierSpend> ModelTierSpend,
    IReadOnlyList<PersonaOpportunityCost> OpportunityCostPerPersona,
    decimal ContestVelocityTouchesPerHour,
    decimal ProspectSaturationPercent);

public sealed record CostPerTouchPoint(DateTimeOffset HourUtc, decimal CostUsd);

public sealed record PersonaOpportunityCost(string PersonaId, string DisplayName, decimal CostUsd);
