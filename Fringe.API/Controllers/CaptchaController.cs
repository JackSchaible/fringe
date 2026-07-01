using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class CaptchaController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    private static readonly string Secret =
        Environment.GetEnvironmentVariable("TURNSTILE_SECRET_KEY")
        ?? throw new InvalidOperationException("TURNSTILE_SECRET_KEY is not set");

    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRequest request)
    {
        var client = httpClientFactory.CreateClient();
        var response = await client.PostAsync(
            "https://challenges.cloudflare.com/turnstile/v0/siteverify",
            new FormUrlEncodedContent([
                new KeyValuePair<string, string>("secret", Secret),
                new KeyValuePair<string, string>("response", request.Token),
            ]));

        var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>();
        return result?.Success == true ? Ok() : BadRequest(new { error = "CAPTCHA verification failed" });
    }

    public record VerifyRequest(string Token);

    private record TurnstileResponse([property: JsonPropertyName("success")] bool Success);
}
