namespace SalesArena.Crm.Scoring;

public sealed record IcpProfile(
    string Name,
    IReadOnlyList<string> TargetIndustries,
    IReadOnlyList<string> TargetRegions,
    int MinHeadcount,
    int MaxHeadcount);

public sealed record LeadSubScores(int Fit, int Intent, int Power);

public sealed record LeadScore(
    int Fit,
    int Intent,
    int Power,
    int Composite,
    IReadOnlyList<string> Rationale);

public sealed record PersonaScoreWeights(double Fit, double Intent, double Power)
{
    public double Normalize()
    {
        var sum = Fit + Intent + Power;
        return sum <= 0 ? 1 : sum;
    }
}
