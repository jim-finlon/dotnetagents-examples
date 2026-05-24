namespace SalesArena.Bench;

public enum BenchErrorCode
{
    ActiveFloorFull = 1,
    NotOnReserve = 2,
    NotActive = 3,
    AlreadyOnRoster = 4,
}

public sealed class BenchException : Exception
{
    public BenchErrorCode Code { get; }
    public BenchException(BenchErrorCode code, string message) : base(message)
    {
        Code = code;
    }
}
