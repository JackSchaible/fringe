using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Fringe.API.Controllers;

/// <summary>Verifies Cloudflare Turnstile CAPTCHA tokens.</summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
internal sealed class CaptchaController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private static readonly string secret =
        Environment.GetEnvironmentVariable("TURNSTILE_SECRET_KEY")
        ?? throw new InvalidOperationException("TURNSTILE_SECRET_KEY is not set");

    /// <summary>Verifies the supplied Turnstile token with Cloudflare.</summary>
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
    {
        using HttpClient client = httpClientFactory.CreateClient();
        using FormUrlEncodedContent content = new([
            new KeyValuePair<string, string>("secret", secret),
            new KeyValuePair<string, string>("response", request.Token),
        ]);
        HttpResponseMessage response = await client.PostAsync(
            new Uri("https://challenges.cloudflare.com/turnstile/v0/siteverify"),
            content).ConfigureAwait(false);

        TurnstileResponse? result = await response.Content.ReadFromJsonAsync<TurnstileResponse>().ConfigureAwait(false);
        return result?.Success == true ? Ok() : BadRequest(new { error = "CAPTCHA verification failed" });
    }

    private record TurnstileResponse([property: JsonPropertyName("success")] bool Success);
}
