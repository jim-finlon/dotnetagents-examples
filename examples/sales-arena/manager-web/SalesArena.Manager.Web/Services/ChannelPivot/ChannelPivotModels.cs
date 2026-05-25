namespace SalesArena.Manager.Web.Services.ChannelPivot;

public enum ChannelPivotMetricMode
{
    ReplyRate,
    CloseRate,
}

public sealed record ChannelPivotPersonaRow(
    string Persona,
    int TouchCount,
    int InboundCount,
    int CloseCount,
    double ReplyRate,
    double CloseRate,
    bool LowSample);

public sealed record ChannelPivotCell(
    string Channel,
    string Industry,
    int TouchCount,
    int InboundCount,
    int CloseCount,
    double ReplyRate,
    double CloseRate,
    bool LowSample,
    IReadOnlyList<ChannelPivotPersonaRow> PersonaRows);

public sealed record ChannelPivotSnapshot(
    string ContestId,
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<string> Channels,
    IReadOnlyList<string> Industries,
    IReadOnlyList<ChannelPivotCell> Cells);
