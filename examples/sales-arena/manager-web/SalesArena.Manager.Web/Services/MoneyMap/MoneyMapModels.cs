namespace SalesArena.Manager.Web.Services.MoneyMap;

public sealed record MoneyMapPin(
    Guid PinId,
    string LeadId,
    string Persona,
    string RegionCode,
    string RegionLabel,
    decimal ValueUsd,
    DateTimeOffset ClosedAtUtc,
    double MapX,
    double MapY,
    string PersonaColor,
    bool IsNew);

public sealed record MoneyMapViewModel(
    IReadOnlyList<MoneyMapPin> UsPins,
    IReadOnlyList<MoneyMapPin> WorldPins,
    string UsMapPath,
    string WorldMapPath,
    decimal TotalRevenueUsd);
