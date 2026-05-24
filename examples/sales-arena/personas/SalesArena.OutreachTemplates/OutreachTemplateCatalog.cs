namespace SalesArena.OutreachTemplates;

/// <summary>
/// Canonical SA-05-05 corpus dimensions (six personas × four channels × three variants).
/// </summary>
public static class OutreachTemplateCatalog
{
    public static readonly IReadOnlyList<string> CanonicalPersonaIds =
    [
        "roma",
        "levene",
        "moss",
        "aaronow",
        "williamson",
        "mitch-and-murray",
    ];

    public static readonly IReadOnlyList<string> Channels =
    [
        "email",
        "sms",
        "linkedin",
        "chat",
    ];

    public static readonly IReadOnlyList<string> VariantIds =
    [
        "variant-1",
        "variant-2",
        "variant-3",
    ];

    public const int ExpectedTemplateCount = 72;
}
