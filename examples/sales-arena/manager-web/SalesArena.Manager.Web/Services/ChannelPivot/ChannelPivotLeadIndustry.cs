namespace SalesArena.Manager.Web.Services.ChannelPivot;

/// <summary>
/// Deterministic industry labels for demo lead ids (L-1000+) until lead snapshots carry industry.
/// </summary>
public static class ChannelPivotLeadIndustry
{
    private static readonly string[] Industries = ["pharma", "saas", "retail", "fintech"];

    public static string Resolve(string leadId)
    {
        if (string.IsNullOrWhiteSpace(leadId))
        {
            return "unknown";
        }

        if (!leadId.StartsWith("L-", StringComparison.Ordinal))
        {
            return "unknown";
        }

        var suffix = leadId.AsSpan(2);
        if (!int.TryParse(suffix, out var numeric))
        {
            return "unknown";
        }

        var index = Math.Abs(numeric - 1000) % Industries.Length;
        return Industries[index];
    }
}
