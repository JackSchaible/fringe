using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

/// <summary>Provides read access to show data.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
internal sealed class ShowsController(FringeRepository repo) : ControllerBase
{
    /// <summary>Returns all shows with their sorted showtimes.</summary>
    [HttpGet]
    public async Task<List<ShowDto>> GetShows()
    {
        List<ShowRecord> showRecords = await repo.GetAllShowsAsync().ConfigureAwait(false);

        IEnumerable<Task<List<ShowTimeRecord>>> showTimeTasks = showRecords.Select(s => repo.GetShowTimesForShowAsync(s.ShowId));
        List<ShowTimeRecord>[] allShowTimes = await Task.WhenAll(showTimeTasks).ConfigureAwait(false);

        return [..showRecords
            .Select((s, i) => ToDto(s, [..allShowTimes[i].Select(st => st.DateTime).Order()]))
            .OrderBy(s => s.Title)];
    }

    /// <summary>Maps a <see cref="ShowRecord"/> to a <see cref="ShowDto"/>.</summary>
    internal static ShowDto ToDto(ShowRecord r, List<string> showTimes)
    {
        return new ShowDto(
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
            showTimes);
    }
}
