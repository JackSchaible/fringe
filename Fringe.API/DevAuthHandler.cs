using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Fringe.API;

/// <summary>Development-only authentication handler that reads the user ID from a request header.</summary>
internal sealed class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <inheritdoc/>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string userId = Request.Headers["X-Dev-User-Id"].FirstOrDefault() ?? "dev-user";
        Claim[] claims = [new Claim("sub", userId), new Claim(ClaimTypes.Name, userId)];
        ClaimsIdentity identity = new(claims, Scheme.Name);
        AuthenticationTicket ticket = new(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
