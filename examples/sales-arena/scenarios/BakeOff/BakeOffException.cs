namespace SalesArena.BakeOff;

public enum BakeOffErrorCode
{
    SameProductBakeOff = 1,
    EmptyProductName = 2,
    EmptyPersonaList = 3,
    EmptyIdealCustomerProfile = 4,
}

public sealed class BakeOffException : Exception
{
    public BakeOffErrorCode Code { get; }
    public BakeOffException(BakeOffErrorCode code, string message) : base(message)
    {
        Code = code;
    }
}
