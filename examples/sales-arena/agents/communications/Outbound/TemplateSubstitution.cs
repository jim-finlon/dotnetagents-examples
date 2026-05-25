namespace SalesArena.Communications.Outbound;

internal static class TemplateSubstitution
{
    public static string Apply(string template, ProspectContext prospect)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(prospect);

        return template
            .Replace("{{prospect.first_name}}", prospect.FirstName, StringComparison.Ordinal)
            .Replace("{{prospect.company}}", prospect.Company, StringComparison.Ordinal)
            .Replace("{{prospect.industry}}", prospect.Industry, StringComparison.Ordinal)
            .Replace("{{prospect.recent_topic}}", prospect.RecentTopic, StringComparison.Ordinal);
    }
}
