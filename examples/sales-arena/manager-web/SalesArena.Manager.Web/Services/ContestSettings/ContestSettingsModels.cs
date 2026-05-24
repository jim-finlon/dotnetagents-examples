using System.ComponentModel.DataAnnotations;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Manager.Web.Services.ContestSettings;

public enum ContestRunState
{
    Idle,
    Initialized,
    Active,
    Paused,
}

public static class PrizeTierPresets
{
    public const string CadillacTier = "CadillacTier";
    public const string SteakKnivesTier = "SteakKnivesTier";
    public const string YouAreFiredTier = "YouAreFiredTier";
    public const string Custom = "Custom";

    public static IReadOnlyList<(string Id, string Label)> Options { get; } =
    [
        (CadillacTier, "Cadillac Tier"),
        (SteakKnivesTier, "Steak Knives Tier"),
        (YouAreFiredTier, "You're Fired Tier"),
        (Custom, "Custom"),
    ];
}

public static class LeadPackOptions
{
    public static IReadOnlyList<(string Id, string Label)> All { get; } =
    [
        ("standard-200", "Standard 200-lead pack"),
        ("glengarry-hot", "Glengarry hot list"),
        ("enterprise-500", "Enterprise 500-lead pack"),
    ];
}

public static class BasePersonaCatalog
{
    public static IReadOnlyList<(string Id, string Label)> All { get; } =
    [
        ("romano", "Romano"),
        ("moss", "Moss"),
        ("aaronow", "Aaronow"),
        ("levene", "Levene"),
        ("williamson", "Williamson"),
        ("harris", "Harris"),
    ];
}

public sealed record ContestRulesDraft(
    bool NoDoubleTouch,
    bool GlengarryDrip,
    bool BellOnClose,
    bool NarrationCues,
    bool BusinessHoursOnly);

public sealed record ContestSettingsDraft(
    string ContestName,
    int DurationHours,
    string LeadPackId,
    IReadOnlyList<string> EnabledPersonas,
    string PrizeTierPreset,
    string ScoringMetricId,
    ContestRulesDraft Rules);

public sealed record ContestLifecycleResult(bool Succeeded, string Message)
{
    public static ContestLifecycleResult Ok(string message) => new(true, message);

    public static ContestLifecycleResult Blocked(string message) => new(false, message);
}

public sealed class ContestSettingsFormModel
{
    [Required(ErrorMessage = "Contest name is required.")]
    [StringLength(80, MinimumLength = 2)]
    public string ContestName { get; set; } = "Glengarry Sprint";

    [Range(1, 168, ErrorMessage = "Duration must be between 1 and 168 hours.")]
    public int DurationHours { get; set; } = 8;

    [Required]
    public string LeadPackId { get; set; } = "standard-200";

    [Required]
    public string PrizeTierPreset { get; set; } = PrizeTierPresets.CadillacTier;

    [Required]
    public string ScoringMetricId { get; set; } = ScoringConfigIds.ByRevenue;

    public bool PersonaRomano { get; set; } = true;
    public bool PersonaMoss { get; set; } = true;
    public bool PersonaAaronow { get; set; } = true;
    public bool PersonaLevene { get; set; } = true;
    public bool PersonaWilliamson { get; set; } = true;
    public bool PersonaHarris { get; set; } = false;

    public bool RuleNoDoubleTouch { get; set; } = true;
    public bool RuleGlengarryDrip { get; set; } = true;
    public bool RuleBellOnClose { get; set; } = true;
    public bool RuleNarrationCues { get; set; } = true;
    public bool RuleBusinessHoursOnly { get; set; } = true;

    public IReadOnlyList<string> GetEnabledPersonas()
    {
        var enabled = new List<string>(capacity: 6);
        if (PersonaRomano) enabled.Add("romano");
        if (PersonaMoss) enabled.Add("moss");
        if (PersonaAaronow) enabled.Add("aaronow");
        if (PersonaLevene) enabled.Add("levene");
        if (PersonaWilliamson) enabled.Add("williamson");
        if (PersonaHarris) enabled.Add("harris");
        return enabled;
    }

    public ContestSettingsDraft ToDraft() => new(
        ContestName.Trim(),
        DurationHours,
        LeadPackId,
        GetEnabledPersonas(),
        PrizeTierPreset,
        ScoringMetricId,
        new ContestRulesDraft(
            RuleNoDoubleTouch,
            RuleGlengarryDrip,
            RuleBellOnClose,
            RuleNarrationCues,
            RuleBusinessHoursOnly));

    public static IReadOnlyList<(string Id, string Label)> ScoringMetricOptions { get; } =
    [
        (ScoringConfigIds.ByRevenue, "Revenue ($)"),
        (ScoringConfigIds.ByDealCount, "Deals Won"),
        (ScoringConfigIds.ByConversion, "Conversion (%)"),
        (ScoringConfigIds.ByAeq, "AEQ Composite"),
    ];
}

public static class ContestSettingsValidation
{
    public const string PersonaRequiredMessage = "Select at least one persona.";

    public static bool HasEnabledPersona(ContestSettingsFormModel model) =>
        model.GetEnabledPersonas().Count > 0;
}
