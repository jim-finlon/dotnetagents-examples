using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace SalesArena.Manager.Web.Hubs;

[Authorize]
public sealed class ArenaHub : Hub
{
    public const string ManagerGroup = "manager-feed";

    public Task JoinManagerFeedAsync() =>
        Groups.AddToGroupAsync(Context.ConnectionId, ManagerGroup);
}
