using System.Collections.ObjectModel;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

/// <summary>Manages the current user's availability windows.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
internal sealed class AvailabilityController(FringeRepository repo) : ControllerBase
{
    /// <summary>Returns the current user's availability windows.</summary>
    [HttpGet]
    public async Task<ActionResult<UserAvailabilityDto>> GetAvailability()
    {
        string userId = GetUserId();
        UserAvailabilityRecord? record = await repo.GetAvailabilityAsync(userId).ConfigureAwait(false);
        IEnumerable<AvailabilityWindowData> rawWindows = record != null
            ? record.Windows
            : [];
        List<AvailabilityWindowDto> windows = [.. rawWindows.Select(w => new AvailabilityWindowDto(w.Start, w.End))];
        return Ok(new UserAvailabilityDto(windows));
    }

    /// <summary>Saves the current user's availability windows.</summary>
    [HttpPut]
    public async Task<ActionResult> SaveAvailability([FromBody] UserAvailabilityDto dto)
    {
        string userId = GetUserId();
        Collection<AvailabilityWindowData> windows = new(
            [.. dto.Windows.Select(w => new AvailabilityWindowData { Start = w.Start, End = w.End })]);
        await repo.SaveAvailabilityAsync(userId, windows).ConfigureAwait(false);
        return Ok();
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value ?? "";
    }
}
