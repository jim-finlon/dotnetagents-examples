namespace SalesArena.Crm.Scoring;

/// <summary>
/// Loads persona-tuned Fit/Intent/Power weights from simple YAML files.
/// </summary>
public sealed class PersonaWeightCatalog
{
    private readonly Dictionary<string, PersonaScoreWeights> _weights;

    public PersonaWeightCatalog(IEnumerable<KeyValuePair<string, PersonaScoreWeights>> entries)
    {
        _weights = new Dictionary<string, PersonaScoreWeights>(StringComparer.OrdinalIgnoreCase);
        foreach (var (persona, weights) in entries)
        {
            _weights[persona] = weights;
        }
    }

    public static PersonaWeightCatalog LoadFromDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Persona weight directory not found: {directory}");
        }

        var entries = new List<KeyValuePair<string, PersonaScoreWeights>>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.yaml"))
        {
            var persona = Path.GetFileNameWithoutExtension(file);
            entries.Add(new KeyValuePair<string, PersonaScoreWeights>(persona, LoadYaml(file)));
        }

        return new PersonaWeightCatalog(entries);
    }

    public PersonaScoreWeights GetWeights(string personaId)
    {
        if (_weights.TryGetValue(personaId, out var weights))
        {
            return weights;
        }

        throw new KeyNotFoundException($"No scoring weights configured for persona '{personaId}'.");
    }

    private static PersonaScoreWeights LoadYaml(string path)
    {
        double fit = 0.34;
        double intent = 0.33;
        double power = 0.33;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var idx = trimmed.IndexOf(':');
            if (idx <= 0)
            {
                continue;
            }

            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();
            if (!double.TryParse(value, out var parsed))
            {
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "fit": fit = parsed; break;
                case "intent": intent = parsed; break;
                case "power": power = parsed; break;
            }
        }

        return new PersonaScoreWeights(fit, intent, power);
    }

    public static int ComputeComposite(LeadSubScores subScores, PersonaScoreWeights weights)
    {
        var norm = weights.Normalize();
        var composite = ((subScores.Fit * weights.Fit)
            + (subScores.Intent * weights.Intent)
            + (subScores.Power * weights.Power)) / norm;
        return (int)Math.Clamp(Math.Round(composite), 0, 100);
    }
}
