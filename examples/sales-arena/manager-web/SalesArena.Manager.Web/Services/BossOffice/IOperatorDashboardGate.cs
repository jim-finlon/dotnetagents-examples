using System.Security.Claims;

namespace SalesArena.Manager.Web.Services.BossOffice;

public interface IOperatorDashboardGate
{
    bool CanAccess(ClaimsPrincipal? user);
}
