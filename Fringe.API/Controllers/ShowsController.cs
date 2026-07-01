using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShowsController(FringeRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<List<ShowDto>> GetShows()
    {
        var showRecords = await repo.GetAllShowsAsync();

        var showTimeTasks = showRecords.Select(s => repo.GetShowTimesForShowAsync(s.ShowId));
        var allShowTimes = await Task.WhenAll(showTimeTasks);

        return showRecords
            .Select((s, i) => ToDto(s, allShowTimes[i].Select(st => st.DateTime).Order().ToList()))
            .OrderBy(s => s.Title)
            .ToList();
    }

    internal static ShowDto ToDto(ShowRecord r, List<string> showTimes) => new(
        r.ShowId,
        r.Title,
        r.Description,
        r.PlainTextDescription,
        r.ImageUrl,
        r.Tag,
        r.Price,
        r.Fee,
        r.LengthInMinutes,
        r.Venue == null ? null : new VenueDto(r.Venue.Name, r.Venue.Address, r.Venue.Phone),
        r.ContentRating == null ? null : new ContentRatingDto(r.ContentRating.Name, r.ContentRating.Code, r.ContentRating.Description),
        showTimes
    );
}
