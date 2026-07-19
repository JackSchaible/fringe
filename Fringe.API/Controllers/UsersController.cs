using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

/// <summary>Manages the current user's profile.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
internal sealed class UsersController(FringeRepository repo, IAmazonCognitoIdentityProvider cognito) : ControllerBase
{
    /// <summary>Returns the current user's profile.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetMe()
    {
        string userId = GetUserId();
        UserRecord? user = await repo.GetUserAsync(userId).ConfigureAwait(false);
        return user == null
            ? NotFound()
            : Ok(new UserDto(userId, user.Email, user.DisplayName, user.GroupId));
    }

    /// <summary>Creates or updates the current user's profile.</summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpsertMe([FromBody] UpsertUserRequest req)
    {
        string userId = GetUserId();
        UserRecord? existing = await repo.GetUserAsync(userId).ConfigureAwait(false);

        UserRecord user = existing ?? new UserRecord { Pk = $"USER#{userId}" };
        user.Email = req.Email;
        user.DisplayName = req.DisplayName;

        await repo.UpsertUserAsync(user).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Updates the current user's display name.</summary>
    [HttpPut("me/display-name")]
    public async Task<IActionResult> UpdateDisplayName([FromBody] UpdateDisplayNameRequest req)
    {
        string userId = GetUserId();
        await repo.UpdateDisplayNameAsync(userId, req.DisplayName.Trim()).ConfigureAwait(false);
        return NoContent();
    }

    /// <summary>Deletes the current user's account and all associated data.</summary>
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMe()
    {
        string userId = GetUserId();
        string username = User.FindFirst("cognito:username")?.Value ?? userId;
        string userPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID") ?? "";

        await repo.DeleteUserDataAsync(userId).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(userPoolId))
        {
            _ = await cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
            {
                UserPoolId = userPoolId,
                Username = username,
            }).ConfigureAwait(false);
        }

        return NoContent();
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value ?? "";
    }
}
