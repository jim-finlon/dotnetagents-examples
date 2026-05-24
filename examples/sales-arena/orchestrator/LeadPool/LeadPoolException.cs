namespace SalesArena.Orchestrator.LeadPool;

/// <summary>
/// Structured error thrown by the lead pool for predictable failure cases.
/// </summary>
public sealed class LeadPoolException : InvalidOperationException
{
    public LeadPoolException(string message, string code) : base(message)
    {
        Code = code;
    }

    public string Code { get; }

    public static class Codes
    {
        public const string PackInvalid = "LEAD_POOL_PACK_INVALID";
        public const string PackNotLoaded = "LEAD_POOL_NOT_LOADED";
        public const string InsufficientAvailable = "LEAD_POOL_INSUFFICIENT_AVAILABLE";
        public const string LeadNotAssignedToPod = "LEAD_POOL_NOT_ASSIGNED_TO_POD";
        public const string LeadUnknown = "LEAD_POOL_LEAD_UNKNOWN";
    }
}
