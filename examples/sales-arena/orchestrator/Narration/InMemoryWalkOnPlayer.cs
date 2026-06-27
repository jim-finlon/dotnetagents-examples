namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// In-memory walk-on dispatcher. Thread-safe for the contest-tick cadence.
/// </summary>
public sealed class InMemoryWalkOnPlayer : IWalkOnPlayer
{
    private readonly Dictionary<string, string> _personaToFile = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private bool _muted;
    private bool _bellRinging;

    public InMemoryWalkOnPlayer(IReadOnlyDictionary<string, string>? initialMap = null, bool startMuted = false)
    {
        if (initialMap is not null)
        {
            foreach (var (persona, path) in initialMap)
            {
                _personaToFile[persona] = path;
            }
        }
        _muted = startMuted;
    }

    public bool IsMuted { get { lock (_lock) return _muted; } }

    public void Mute() { lock (_lock) _muted = true; }
    public void Unmute() { lock (_lock) _muted = false; }

    public void NotifyBellStart() { lock (_lock) _bellRinging = true; }
    public void NotifyBellEnd() { lock (_lock) _bellRinging = false; }

    public void RegisterWalkOn(string persona, string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        lock (_lock) _personaToFile[persona] = filePath;
    }

    public (WalkOnDecision Decision, WalkOnRequest? Request) Play(string persona, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrEmpty(persona);
        lock (_lock)
        {
            if (!_personaToFile.TryGetValue(persona, out var path))
            {
                return (WalkOnDecision.NoWalkOnForPersona, null);
            }
            var request = new WalkOnRequest(persona, path, now);
            if (_muted)
            {
                return (WalkOnDecision.Muted, request);
            }
            if (_bellRinging)
            {
                return (WalkOnDecision.DeferredForBell, request);
            }
            return (WalkOnDecision.Played, request);
        }
    }

    /// <summary>
    /// Default per-persona file mapping pointing at the six SA-08-10
    /// walk-ons shipped under <c>examples/sales-arena/assets/audio/walk-ons/</c>.
    /// Callers can override individual entries via <see cref="RegisterWalkOn"/>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> DefaultMap(string walkOnsBaseDir) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["roma"] = Path.Combine(walkOnsBaseDir, "roma.wav"),
            ["levene"] = Path.Combine(walkOnsBaseDir, "levene.wav"),
            ["moss"] = Path.Combine(walkOnsBaseDir, "moss.wav"),
            ["aaronow"] = Path.Combine(walkOnsBaseDir, "aaronow.wav"),
            ["williamson"] = Path.Combine(walkOnsBaseDir, "williamson.wav"),
            ["mitch-and-murray"] = Path.Combine(walkOnsBaseDir, "mitch-and-murray.wav"),
        };
}
