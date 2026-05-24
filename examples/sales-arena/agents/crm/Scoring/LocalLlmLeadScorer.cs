namespace SalesArena.Crm.Scoring;

/// <summary>
/// Default <see cref="ILeadScorer"/> using a rubric prompt template and injectable score model.
/// Premium routing is a pass-through flag only (set --routing-mode=premium to enable AEQ-based model selection).
/// </summary>
public sealed class LocalLlmLeadScorer : ILeadScorer
{
    private readonly ILeadScoreModel _model;
    private readonly PersonaWeightCatalog _weights;
    private readonly string _rubricPrompt;
    private readonly bool _usePremiumRouting;

    public LocalLlmLeadScorer(
        ILeadScoreModel model,
        PersonaWeightCatalog weights,
        string rubricPromptPath,
        bool usePremiumRouting = false)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
        ArgumentException.ThrowIfNullOrWhiteSpace(rubricPromptPath);
        if (!File.Exists(rubricPromptPath))
        {
            throw new FileNotFoundException("Scoring rubric prompt not found.", rubricPromptPath);
        }

        _rubricPrompt = File.ReadAllText(rubricPromptPath);
        _usePremiumRouting = usePremiumRouting;
    }

    public bool UsePremiumRouting => _usePremiumRouting;

    public async Task<LeadScore> ScoreAsync(
        CrmRecord lead,
        IcpProfile icp,
        string personaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lead);
        ArgumentNullException.ThrowIfNull(icp);
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);

        var prompt = _rubricPrompt
            + $"\n\n# runtime\npremium_routing={_usePremiumRouting}\npersona={personaId}\nlead={lead.LeadId}";

        var (subScores, rationale) = await _model
            .ScoreAsync(lead, icp, prompt, cancellationToken)
            .ConfigureAwait(false);
        var personaWeights = _weights.GetWeights(personaId);
        var composite = PersonaWeightCatalog.ComputeComposite(subScores, personaWeights);

        var mergedRationale = rationale
            .Concat(
            [
                $"Composite {composite} using persona weights fit={personaWeights.Fit:0.##}, intent={personaWeights.Intent:0.##}, power={personaWeights.Power:0.##}.",
            ])
            .ToList();

        return new LeadScore(subScores.Fit, subScores.Intent, subScores.Power, composite, mergedRationale);
    }
}
