namespace SalesArena.HotStove;

public enum HotStoveErrorCode
{
    OperatorApprovalRequired = 1,
    TradeCooldownActive = 2,
    SamePersonaTrade = 3,
    UnknownDraft = 4,
    DraftAlreadyPromoted = 5,
    EmptyTemplateRef = 6,
    EmptyContestId = 7,
    UnknownTrade = 8,
    TradeAlreadyApplied = 9,
}

public sealed class HotStoveException : Exception
{
    public HotStoveErrorCode Code { get; }
    public HotStoveException(HotStoveErrorCode code, string message) : base(message)
    {
        Code = code;
    }
}
