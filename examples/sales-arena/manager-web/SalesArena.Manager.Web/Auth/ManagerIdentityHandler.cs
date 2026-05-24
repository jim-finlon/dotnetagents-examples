using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SalesArena.Manager.Web.Auth;

/// <summary>
/// Single-operator local demo: every request is the in-process "manager" identity.
/// Real login UI is deferred to SA-06.
/// </summary>
public sealed class ManagerIdentityHandler : AuthenticationHandler<ManagerIdentityOptions>
{
    public ManagerIdentityHandler(
        IOptionsMonitor<ManagerIdentityOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(Scheme.Name);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, ManagerIdentityDefaults.OperatorId));
        identity.AddClaim(new Claim(ClaimTypes.Name, ManagerIdentityDefaults.OperatorId));
        identity.AddClaim(new Claim(ClaimTypes.Role, ManagerIdentityDefaults.OperatorRole));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
