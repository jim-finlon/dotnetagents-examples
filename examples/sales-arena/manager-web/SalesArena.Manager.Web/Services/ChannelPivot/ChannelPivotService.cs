using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Manager.Web.Services.ChannelPivot;

public sealed class ChannelPivotService : IChannelPivotService
{
    private const int LowSampleThreshold = 5;
    private const string DefaultContestId = "demo-contest";

    private static readonly string[] ChannelOrder = ["email", "sms", "linkedin", "chat"];
    private static readonly string[] IndustryOrder = ["pharma", "saas", "retail", "fintech", "unknown"];

    private readonly IArenaLedger _ledger;

    public ChannelPivotService(IArenaLedger ledger) => _ledger = ledger;

    public async Task<ChannelPivotSnapshot> BuildSnapshotAsync(
        string? contestId = null,
        CancellationToken cancellationToken = default)
    {
        contestId ??= DefaultContestId;
        var buckets = new Dictionary<(string Channel, string Industry, string Persona), MutableBucket>();

        await foreach (var evt in _ledger.QueryAsync(
                           new ArenaEventFilter { ContestId = contestId },
                           cancellationToken).ConfigureAwait(false))
        {
            switch (evt.Kind)
            {
                case ArenaEventKinds.TouchSent:
                {
                    var p = evt.GetPayload<TouchSentPayload>();
                    if (p is null)
                    {
                        break;
                    }

                    var industry = ChannelPivotLeadIndustry.Resolve(p.LeadId);
                    var key = (NormalizeChannel(p.Channel), industry, evt.Persona ?? p.Persona);
                    GetBucket(buckets, key).Touches++;
                    break;
                }

                case ArenaEventKinds.InboundReceived:
                {
                    var p = evt.GetPayload<InboundReceivedPayload>();
                    if (p is null)
                    {
                        break;
                    }

                    var industry = ChannelPivotLeadIndustry.Resolve(p.LeadId);
                    var key = (NormalizeChannel(p.Channel), industry, evt.Persona ?? p.Persona);
                    GetBucket(buckets, key).Inbounds++;
                    break;
                }

                case ArenaEventKinds.DealClosed:
                {
                    var p = evt.GetPayload<DealClosedPayload>();
                    if (p is null || !string.Equals(p.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    var industry = ChannelPivotLeadIndustry.Resolve(p.LeadId);
                    var channel = await ResolveChannelForLeadAsync(
                        contestId,
                        p.LeadId,
                        evt.Persona ?? p.Persona,
                        cancellationToken).ConfigureAwait(false);
                    var key = (channel, industry, evt.Persona ?? p.Persona);
                    GetBucket(buckets, key).Closes++;
                    break;
                }
            }
        }

        var cells = new List<ChannelPivotCell>();
        var channels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var industries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in buckets.GroupBy(b => (b.Key.Channel, b.Key.Industry)))
        {
            var touchTotal = group.Sum(g => g.Value.Touches);
            var inboundTotal = group.Sum(g => g.Value.Inbounds);
            var closeTotal = group.Sum(g => g.Value.Closes);
            var personaRows = group
                .Select(g => ToPersonaRow(g.Key.Persona, g.Value))
                .OrderByDescending(r => r.TouchCount)
                .ToList();

            var cell = new ChannelPivotCell(
                group.Key.Channel,
                group.Key.Industry,
                touchTotal,
                inboundTotal,
                closeTotal,
                Rate(inboundTotal, touchTotal),
                Rate(closeTotal, touchTotal),
                touchTotal < LowSampleThreshold,
                personaRows);

            cells.Add(cell);
            channels.Add(cell.Channel);
            industries.Add(cell.Industry);
        }

        return new ChannelPivotSnapshot(
            contestId,
            DateTimeOffset.UtcNow,
            OrderChannels(channels),
            OrderIndustries(industries),
            cells.OrderBy(c => c.Channel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.Industry, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    public string ExportCsv(ChannelPivotSnapshot snapshot, ChannelPivotMetricMode metricMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("channel,industry,persona,touches,inbounds,closes,reply_rate,close_rate,low_sample,metric_mode,metric_value");
        foreach (var cell in snapshot.Cells)
        {
            foreach (var row in cell.PersonaRows)
            {
                AppendCsvRow(sb, cell.Channel, cell.Industry, row, metricMode);
            }

            if (cell.PersonaRows.Count == 0)
            {
                sb.Append(CsvEscape(cell.Channel)).Append(',')
                    .Append(CsvEscape(cell.Industry)).Append(',')
                    .Append(',')
                    .Append(cell.TouchCount).Append(',')
                    .Append(cell.InboundCount).Append(',')
                    .Append(cell.CloseCount).Append(',')
                    .Append(cell.ReplyRate.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                    .Append(cell.CloseRate.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                    .Append(cell.LowSample ? "true" : "false").Append(',')
                    .Append(metricMode).Append(',')
                    .Append(MetricValue(cell, metricMode).ToString("F4", CultureInfo.InvariantCulture))
                    .AppendLine();
            }
        }

        return sb.ToString();
    }

    private static void AppendCsvRow(
        StringBuilder sb,
        string channel,
        string industry,
        ChannelPivotPersonaRow row,
        ChannelPivotMetricMode metricMode)
    {
        sb.Append(CsvEscape(channel)).Append(',')
            .Append(CsvEscape(industry)).Append(',')
            .Append(CsvEscape(row.Persona)).Append(',')
            .Append(row.TouchCount).Append(',')
            .Append(row.InboundCount).Append(',')
            .Append(row.CloseCount).Append(',')
            .Append(row.ReplyRate.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
            .Append(row.CloseRate.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
            .Append(row.LowSample ? "true" : "false").Append(',')
            .Append(metricMode).Append(',')
            .Append(MetricValue(row, metricMode).ToString("F4", CultureInfo.InvariantCulture))
            .AppendLine();
    }

    private async Task<string> ResolveChannelForLeadAsync(
        string contestId,
        string leadId,
        string persona,
        CancellationToken cancellationToken)
    {
        await foreach (var evt in _ledger.QueryAsync(
                           new ArenaEventFilter { ContestId = contestId, LeadId = leadId },
                           cancellationToken).ConfigureAwait(false))
        {
            if (evt.Kind == ArenaEventKinds.TouchSent)
            {
                var p = evt.GetPayload<TouchSentPayload>();
                if (p is not null)
                {
                    return NormalizeChannel(p.Channel);
                }
            }
        }

        return "email";
    }

    private static MutableBucket GetBucket(
        Dictionary<(string Channel, string Industry, string Persona), MutableBucket> buckets,
        (string Channel, string Industry, string Persona) key) =>
        buckets.TryGetValue(key, out var bucket)
            ? bucket
            : buckets[key] = new MutableBucket();

    private static ChannelPivotPersonaRow ToPersonaRow(string persona, MutableBucket bucket)
    {
        var touches = bucket.Touches;
        return new ChannelPivotPersonaRow(
            persona,
            touches,
            bucket.Inbounds,
            bucket.Closes,
            Rate(bucket.Inbounds, touches),
            Rate(bucket.Closes, touches),
            touches < LowSampleThreshold);
    }

    private static double Rate(int numerator, int denominator) =>
        denominator <= 0 ? 0 : (double)numerator / denominator;

    private static double MetricValue(ChannelPivotCell cell, ChannelPivotMetricMode mode) =>
        mode == ChannelPivotMetricMode.CloseRate ? cell.CloseRate : cell.ReplyRate;

    private static double MetricValue(ChannelPivotPersonaRow row, ChannelPivotMetricMode mode) =>
        mode == ChannelPivotMetricMode.CloseRate ? row.CloseRate : row.ReplyRate;

    private static string NormalizeChannel(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? "unknown" : channel.Trim().ToLowerInvariant();

    private static IReadOnlyList<string> OrderChannels(IEnumerable<string> channels)
    {
        var set = new HashSet<string>(channels, StringComparer.OrdinalIgnoreCase);
        var ordered = ChannelOrder.Where(set.Contains).ToList();
        ordered.AddRange(set.Except(ordered, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    private static IReadOnlyList<string> OrderIndustries(IEnumerable<string> industries)
    {
        var set = new HashSet<string>(industries, StringComparer.OrdinalIgnoreCase);
        var ordered = IndustryOrder.Where(set.Contains).ToList();
        ordered.AddRange(set.Except(ordered, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase));
        return ordered;
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }

    private sealed class MutableBucket
    {
        public int Touches;
        public int Inbounds;
        public int Closes;
    }
}
