using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AvailabilityController(FringeRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserAvailabilityDto>> GetAvailability()
    {
        string userId = GetUserId();
        var record = await repo.GetAvailabilityAsync(userId);
        var windows = (record?.Windows ?? [])
            .Select(w => new AvailabilityWindowDto(w.Start, w.End))
            .ToList();
        return Ok(new UserAvailabilityDto(windows));
    }

    [HttpPut]
    public async Task<ActionResult> SaveAvailability([FromBody] UserAvailabilityDto dto)
    {
        string userId = GetUserId();
        var windows = dto.Windows
            .Select(w => new AvailabilityWindowData { Start = w.Start, End = w.End })
            .ToList();
        await repo.SaveAvailabilityAsync(userId, windows);
        return Ok();
    }

    private string GetUserId() => User.FindFirst("sub")?.Value ?? "";
}
