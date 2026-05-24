using System.Collections.Concurrent;

namespace SalesArena.Orchestrator.Orchestration;

public sealed class PodManager : IPodManager
{
    private readonly PersonaPodFactory _factory;
    private readonly ConcurrentDictionary<string, PersonaPod> _pods = new(StringComparer.OrdinalIgnoreCase);

    public PodManager(PersonaPodFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public IReadOnlyCollection<PersonaPod> ActivePods => _pods.Values.OrderBy(pod => pod.PodId, StringComparer.Ordinal).ToArray();

    public PersonaPod SpawnPod(string persona)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(persona);

        var slug = Slugify(persona);
        var podId = $"{slug}-{Guid.NewGuid():N}"[..(slug.Length + 9)];
        var pod = _factory(podId, persona);
        if (!_pods.TryAdd(pod.PodId, pod))
        {
            throw new InvalidOperationException($"Pod '{pod.PodId}' already exists.");
        }

        return pod;
    }

    public bool Despawn(string podId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(podId);
        return _pods.TryRemove(podId, out _);
    }

    private static string Slugify(string value)
    {
        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "pod" : slug;
    }
}
