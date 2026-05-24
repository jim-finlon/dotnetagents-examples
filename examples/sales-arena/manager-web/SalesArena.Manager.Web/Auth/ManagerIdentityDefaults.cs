namespace SalesArena.Manager.Web.Auth;

public static class ManagerIdentityDefaults
{
    public const string Scheme = "ArenaManagerStub";
    public const string OperatorId = "manager";
    public const string OperatorRole = "arena-manager";

    /// <summary>Spectator role for future SA-06 login; Boss Office denies this role.</summary>
    public const string SpectatorRole = "arena-spectator";
}
