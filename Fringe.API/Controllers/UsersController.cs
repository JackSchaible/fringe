using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(FringeRepository repo, IAmazonCognitoIdentityProvider cognito) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        string userId = GetUserId();
        var user = await repo.GetUserAsync(userId);
        if (user == null) return NotFound();
        return Ok(new UserDto(userId, user.Email, user.DisplayName, user.GroupId));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpsertMe([FromBody] UpsertUserRequest req)
    {
        string userId = GetUserId();
        var existing = await repo.GetUserAsync(userId);

        var user = existing ?? new UserRecord { Pk = $"USER#{userId}" };
        user.Email = req.Email;
        user.DisplayName = req.DisplayName;

        await repo.UpsertUserAsync(user);
        return NoContent();
    }

    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe()
    {
        string userId = GetUserId();
        string username = User.FindFirst("cognito:username")?.Value ?? userId;
        string userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID") ?? "";

        await repo.DeleteUserDataAsync(userId);

        if (!string.IsNullOrEmpty(userPoolId))
        {
            await cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
            {
                UserPoolId = userPoolId,
                Username = username,
            });
        }

        return NoContent();
    }

    private string GetUserId() => User.FindFirst("sub")?.Value ?? "";
}
