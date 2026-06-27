namespace SalesArena.OutreachTemplates;

/// <summary>
/// Loads SA-05-05 outreach templates from <c>examples/sales-arena/personas</c>.
/// Consumed by SA-01-07 A/B promotion harness.
/// </summary>
public interface IOutreachTemplateLoader
{
    /// <summary>
    /// Loads all templates for the six canonical flagship personas.
    /// </summary>
    IReadOnlyList<OutreachTemplateRecord> LoadAll();

    /// <summary>
    /// Loads templates for one persona across all channels and variants.
    /// </summary>
    IReadOnlyList<OutreachTemplateRecord> LoadForPersona(string personaId);
}
