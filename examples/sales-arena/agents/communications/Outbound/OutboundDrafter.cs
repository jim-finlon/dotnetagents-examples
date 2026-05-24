using SalesArena.OutreachTemplates;

namespace SalesArena.Communications.Outbound;

public sealed class OutboundDrafter(
    IOutreachTemplateLoader templateLoader,
    AbPromotionTracker promotionTracker) : IOutboundDrafter
{
    private readonly IOutreachTemplateLoader _templateLoader =
        templateLoader ?? throw new ArgumentNullException(nameof(templateLoader));

    private readonly AbPromotionTracker _promotionTracker =
        promotionTracker ?? throw new ArgumentNullException(nameof(promotionTracker));

    public Task<DraftMessage> DraftAsync(
        ProspectContext prospect,
        string personaId,
        string channel,
        OutboundIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prospect);
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        _ = cancellationToken;

        if (!OutreachTemplateCatalog.Channels.Contains(channel, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unknown channel '{channel}'.", nameof(channel));
        }

        var templates = _templateLoader.LoadForPersona(personaId);
        var channelTemplates = templates
            .Where(t => string.Equals(t.Channel, channel, StringComparison.Ordinal))
            .ToList();
        if (channelTemplates.Count == 0)
        {
            throw new InvalidOperationException($"No templates for persona '{personaId}' channel '{channel}'.");
        }

        var variantIds = channelTemplates.Select(t => t.VariantId).ToList();
        var selectedVariant = _promotionTracker.SelectVariantForSend(personaId, channel, variantIds);
        var template = channelTemplates.First(t =>
            string.Equals(t.VariantId, selectedVariant, StringComparison.Ordinal));

        var body = TemplateSubstitution.Apply(template.BodyMarkdown, prospect);
        var (subject, messageBody) = ExtractSubject(channel, body);
        var confidence = ComputeConfidence(template.WordCountTarget, messageBody, intent);

        var draft = new DraftMessage(
            personaId,
            channel,
            template.VariantId,
            subject,
            messageBody,
            confidence,
            template.Hypothesis);

        return Task.FromResult(draft);
    }

    private static (string? Subject, string Body) ExtractSubject(string channel, string body)
    {
        if (!string.Equals(channel, "email", StringComparison.Ordinal))
        {
            return (null, body.Trim());
        }

        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length > 0 && lines[0].StartsWith("Subject:", StringComparison.OrdinalIgnoreCase))
        {
            var subject = lines[0]["Subject:".Length..].Trim();
            var remainder = string.Join('\n', lines.Skip(1)).Trim();
            return (subject, remainder);
        }

        return (null, body.Trim());
    }

    private static double ComputeConfidence(int wordTarget, string body, OutboundIntent intent)
    {
        var words = body.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        var proximity = wordTarget <= 0
            ? 1.0
            : 1.0 - Math.Min(1.0, Math.Abs(words - wordTarget) / (double)wordTarget);
        var intentBoost = intent switch
        {
            OutboundIntent.Introduce => 0.02,
            OutboundIntent.FollowUp => 0.04,
            OutboundIntent.MeetingNudge => 0.03,
            OutboundIntent.ReEngage => 0.01,
            _ => 0,
        };
        return Math.Clamp(0.65 + (proximity * 0.25) + intentBoost, 0.0, 1.0);
    }
}
