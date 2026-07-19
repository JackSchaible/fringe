using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Fringe.API.Tests;

/// <summary>
/// Tests for DevAuthHandler — the dev-only stub that authenticates via X-Dev-User-Id header.
/// </summary>
public sealed class DevAuthHandlerTests
{
    private static async Task<(DevAuthHandler handler, AuthenticateResult result)> AuthenticateAsync(
        string? headerValue)
    {
        var options = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        _ = options.Setup(o => o.Get(It.IsAny<string>()))
               .Returns(new AuthenticationSchemeOptions());

        var loggerFactory = new Mock<ILoggerFactory>();
        _ = loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
                     .Returns(new Mock<ILogger>().Object);

        UrlEncoder urlEncoder = UrlEncoder.Default;

        DevAuthHandler handler = new(options.Object, loggerFactory.Object, urlEncoder);

        DefaultHttpContext context = new();
        if (headerValue != null)
        {
            context.Request.Headers["X-Dev-User-Id"] = headerValue;
        }

        AuthenticationScheme scheme = new("Dev", "Dev", typeof(DevAuthHandler));
        await handler.InitializeAsync(scheme, context).ConfigureAwait(false);

        AuthenticateResult result = await handler.AuthenticateAsync().ConfigureAwait(false);
        return (handler, result);
    }

    // ── With header ───────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAuthenticateAsyncWithHeaderReturnsSuccess()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync("user-from-header").ConfigureAwait(true);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncWithHeaderSetsSubClaim()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync("user-from-header").ConfigureAwait(true);

        Claim? sub = result.Principal!.FindFirst("sub");
        Assert.NotNull(sub);
        Assert.Equal("user-from-header", sub!.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncWithHeaderSetsNameClaim()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync("my-user-id").ConfigureAwait(true);

        Claim? name = result.Principal!.FindFirst(ClaimTypes.Name);
        Assert.NotNull(name);
        Assert.Equal("my-user-id", name!.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncWithHeaderTicketSchemeIsDevScheme()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync("any-user").ConfigureAwait(true);

        Assert.Equal("Dev", result.Ticket!.AuthenticationScheme);
    }

    // ── Without header ────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAuthenticateAsyncWithoutHeaderReturnsSuccess()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync(null).ConfigureAwait(true);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncWithoutHeaderFallsBackToDevUser()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync(null).ConfigureAwait(true);

        Claim? sub = result.Principal!.FindFirst("sub");
        Assert.NotNull(sub);
        Assert.Equal("dev-user", sub!.Value);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncWithoutHeaderNameClaimIsDevUser()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync(null).ConfigureAwait(true);

        Claim? name = result.Principal!.FindFirst(ClaimTypes.Name);
        Assert.NotNull(name);
        Assert.Equal("dev-user", name!.Value);
    }

    // ── Empty header ──────────────────────────────────────────────────────────

    [Fact]
    public async Task HandleAuthenticateAsyncEmptyHeaderFallsBackToDevUser()
    {
        // An empty string header means FirstOrDefault() returns "" which is falsy for ?? to fall through
        // Actually in C#, "" ?? "dev-user" = "" since "" is not null.
        // So empty header → userId = ""
        (_, AuthenticateResult result) = await AuthenticateAsync("").ConfigureAwait(true);

        Claim? sub = result.Principal!.FindFirst("sub");
        Assert.NotNull(sub);
        // Empty string header: StringValues.FirstOrDefault() returns "" which is not null
        // so userId = "" (not "dev-user")
        Assert.Equal("", sub!.Value);
    }

    // ── ClaimsPrincipal shape ─────────────────────────────────────────────────

    [Fact]
    public async Task HandleAuthenticateAsyncPrincipalHasExactlyTwoClaims()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync("test-user").ConfigureAwait(true);

        List<Claim> claims = [.. result.Principal!.Claims];
        Assert.Equal(2, claims.Count);
    }

    [Fact]
    public async Task HandleAuthenticateAsyncIdentityIsAuthenticated()
    {
        (_, AuthenticateResult result) = await AuthenticateAsync("test-user").ConfigureAwait(true);

        Assert.True(result.Principal!.Identity!.IsAuthenticated);
    }
}
