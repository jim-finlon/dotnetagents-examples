namespace SalesArena.Crm;

/// <summary>
/// Structured error thrown when a CRM transition is illegal or a record is
/// missing. Carries a stable <see cref="Code"/> so callers can branch without
/// pattern-matching exception messages.
/// </summary>
public sealed class CrmStateException : InvalidOperationException
{
    public CrmStateException(string message, string code) : base(message)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable error code (e.g. <c>CRM_ILLEGAL_TRANSITION</c>).</summary>
    public string Code { get; }

    public static class Codes
    {
        public const string IllegalTransition = "CRM_ILLEGAL_TRANSITION";
        public const string UnknownStage = "CRM_UNKNOWN_STAGE";
        public const string TerminalStage = "CRM_TERMINAL_STAGE";
        public const string ActivityLogClosed = "CRM_ACTIVITY_LOG_CLOSED";
    }
}
