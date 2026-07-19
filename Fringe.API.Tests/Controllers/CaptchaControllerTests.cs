using Fringe.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Fringe.API.Tests.Controllers;

/// <summary>
/// Tests for CaptchaController.
///
/// CaptchaController reads TURNSTILE_SECRET_KEY from environment in a static field
/// initializer. The static initializer runs once per AppDomain, so we ensure the
/// env var is set before instantiating the controller for the first time.
/// </summary>
public sealed class CaptchaControllerTests : IDisposable
{
    private HttpClient? httpClient;

    static CaptchaControllerTests()
    {
        // Must be set before CaptchaController's static field is initialized.
        Environment.SetEnvironmentVariable("TURNSTILE_SECRET_KEY", "test-secret");
    }

    private CaptchaController BuildController(HttpMessageHandler handler)
    {
        httpClient = new HttpClient(handler);
        var mockFactory = new Mock<IHttpClientFactory>();
        _ = mockFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);
        return new CaptchaController(mockFactory.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static string MakeTurnstileJson(bool success)
    {
        return JsonSerializer.Serialize(new { success });
    }

    // ── Verify ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyTurnstileSuccessTrueReturnsOk()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(MakeTurnstileJson(true), Encoding.UTF8, "application/json")
        };
        using FakeHttpMessageHandler fakeHandler = new(response);
        CaptchaController controller = BuildController(fakeHandler);

        IActionResult result = await controller.Verify(new VerifyRequest("valid-token")).ConfigureAwait(true);

        _ = Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task VerifyTurnstileSuccessFalseReturnsBadRequest()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(MakeTurnstileJson(false), Encoding.UTF8, "application/json")
        };
        using FakeHttpMessageHandler fakeHandler = new(response);
        CaptchaController controller = BuildController(fakeHandler);

        IActionResult result = await controller.Verify(new VerifyRequest("invalid-token")).ConfigureAwait(true);

        BadRequestObjectResult bad = Assert.IsType<BadRequestObjectResult>(result);
        // Response body has an "error" property
        Assert.NotNull(bad.Value);
        string json = JsonSerializer.Serialize(bad.Value);
        Assert.Contains("error", json, StringComparison.Ordinal);
        Assert.Contains("CAPTCHA", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyTurnstileReturnsNullBodyReturnsBadRequest()
    {
        // Empty response body → deserialization yields null TurnstileResponse → !success
        using HttpResponseMessage emptyResponse = new(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };
        using FakeHttpMessageHandler fakeHandler = new(emptyResponse);
        CaptchaController controller = BuildController(fakeHandler);

        IActionResult result = await controller.Verify(new VerifyRequest("any-token")).ConfigureAwait(true);

        _ = Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task VerifyPostsTokenToCloudflareEndpoint()
    {
        HttpRequestMessage? captured = null;
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(MakeTurnstileJson(true), Encoding.UTF8, "application/json")
        };
        using CapturingHttpMessageHandler spyHandler = new(req => { captured = req; return response; });
        CaptchaController controller = BuildController(spyHandler);

        _ = await controller.Verify(new VerifyRequest("my-token")).ConfigureAwait(true);

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Contains("challenges.cloudflare.com", captured.RequestUri?.Host, StringComparison.Ordinal);
        Assert.Contains("my-token", spyHandler.Body, StringComparison.Ordinal);
        Assert.Contains("test-secret", spyHandler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyEmptyTokenStillCallsTurnstile()
    {
        HttpRequestMessage? captured = null;
        using HttpResponseMessage response = new(HttpStatusCode.OK)
        {
            Content = new StringContent(MakeTurnstileJson(false), Encoding.UTF8, "application/json")
        };
        using CapturingHttpMessageHandler spyHandler = new(req => { captured = req; return response; });
        CaptchaController controller = BuildController(spyHandler);

        IActionResult result = await controller.Verify(new VerifyRequest("")).ConfigureAwait(true);

        _ = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(captured); // Turnstile was still called
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        httpClient?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> sendFunc;

        internal CapturingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sendFunc)
        {
            this.sendFunc = sendFunc;
        }

        internal string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            return sendFunc(request);
        }
    }
}
