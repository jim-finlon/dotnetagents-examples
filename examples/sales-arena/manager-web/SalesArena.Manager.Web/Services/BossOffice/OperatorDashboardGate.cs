using System.Security.Claims;
using SalesArena.Manager.Web.Auth;

namespace SalesArena.Manager.Web.Services.BossOffice;

public sealed class OperatorDashboardGate : IOperatorDashboardGate
{
    public bool CanAccess(ClaimsPrincipal? user) =>
        user?.IsInRole(ManagerIdentityDefaults.OperatorRole) == true;
}
