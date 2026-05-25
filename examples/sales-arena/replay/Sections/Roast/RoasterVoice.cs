namespace SalesArena.Replay.Sections.Roast;

/// <summary>
/// Voice profiles the stub roast writer can pick from. Real-LLM writers
/// (post-SA-05-01) read the roaster's persona system prompt directly; this
/// enum is the no-persona-pack-yet fallback so the stub is still
/// distinctive across roasters.
/// </summary>
public enum RoasterVoice
{
    /// <summary>Patient, mythic, polite-but-cutting (Roma-flavored).</summary>
    Elegant,

    /// <summary>Blunt, urgent, no-nonsense (Levene-flavored).</summary>
    Blunt,

    /// <summary>Surgical, evidence-anchored, technically dispassionate (Moss-flavored).</summary>
    Surgical,

    /// <summary>Steady, warm, never twists the knife (Aaronow-flavored).</summary>
    Steady,
}

/// <summary>
/// Maps a persona id to a baseline RoasterVoice. Operators can override via
/// custom IRoastWriter; this map is the safe default until persona packs
/// (SA-05-01) ship.
/// </summary>
public static class RoasterVoiceMap
{
    private static readonly IReadOnlyDictionary<string, RoasterVoice> KnownPersonas =
        new Dictionary<string, RoasterVoice>(StringComparer.OrdinalIgnoreCase)
        {
            ["roma"] = RoasterVoice.Elegant,
            ["levene"] = RoasterVoice.Blunt,
            ["moss"] = RoasterVoice.Surgical,
            ["aaronow"] = RoasterVoice.Steady,
            ["williamson"] = RoasterVoice.Steady,
            ["mitch-and-murray"] = RoasterVoice.Surgical,
        };

    public static RoasterVoice For(string persona) =>
        KnownPersonas.TryGetValue(persona, out var voice) ? voice : RoasterVoice.Steady;
}
