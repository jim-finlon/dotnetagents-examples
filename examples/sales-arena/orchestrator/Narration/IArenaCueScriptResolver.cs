namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Picks a script line for a cue. Default behavior: deterministic round-robin
/// over the lines in a cue's template, with token substitution
/// (`{persona}`, `{lead}`, `{value}` …).
/// </summary>
public interface IArenaCueScriptResolver
{
    /// <returns>The resolved script line, or null if no template exists for the cue.</returns>
    string? Resolve(string cueKind, IReadOnlyDictionary<string, string> tokens);
}
