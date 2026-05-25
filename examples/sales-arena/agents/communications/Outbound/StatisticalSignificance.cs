namespace SalesArena.Communications.Outbound;

/// <summary>
/// Two-proportion z-test for A/B promotion gates (SA-01-07).
/// </summary>
internal static class StatisticalSignificance
{
    private const double ZCritical95 = 1.96;

    public static bool IsSignificantlyHigher(int successesA, int trialsA, int successesB, int trialsB)
    {
        if (trialsA < 1 || trialsB < 1)
        {
            return false;
        }

        var pA = (double)successesA / trialsA;
        var pB = (double)successesB / trialsB;
        if (pA <= pB)
        {
            return false;
        }

        var pooled = (double)(successesA + successesB) / (trialsA + trialsB);
        var se = Math.Sqrt(pooled * (1 - pooled) * ((1.0 / trialsA) + (1.0 / trialsB)));
        if (se <= 0)
        {
            return false;
        }

        var z = (pA - pB) / se;
        return z >= ZCritical95;
    }
}
