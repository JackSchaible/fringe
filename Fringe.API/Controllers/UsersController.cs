using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController(FringeRepository repo) : ControllerBase
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

    private string GetUserId() => User.FindFirst("sub")?.Value ?? "";
}
