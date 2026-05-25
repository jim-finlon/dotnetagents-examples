namespace SalesArena.OutreachTemplates;

/// <summary>
/// One persona/channel/variant outreach template loaded from disk.
/// </summary>
public sealed record OutreachTemplateRecord(
    string PersonaId,
    string Channel,
    string VariantId,
    string Hypothesis,
    int WordCountTarget,
    IReadOnlyList<string> SubstitutionTokens,
    string BodyMarkdown,
    bool NarratorOnly);
