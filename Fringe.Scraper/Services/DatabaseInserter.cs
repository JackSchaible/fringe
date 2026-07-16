using Fringe.Data;
using Fringe.Data.Models;

namespace FringeScraper.Services;

/// <summary>Inserts scraped show data into DynamoDB via <see cref="FringeRepository"/>.</summary>
internal sealed class DatabaseInserter(FringeRepository repository)
{
    /// <summary>Saves shows and their showtimes to DynamoDB.</summary>
    public async Task InsertDataAsync(IEnumerable<Show> shows, IEnumerable<ShowTime> showTimes)
    {
        ScraperLogger.Log("Inserting shows into DynamoDB...");
        await repository.SaveShowsAsync(shows).ConfigureAwait(false);

        ScraperLogger.Log("Inserting showtimes into DynamoDB...");
        await repository.SaveShowTimesAsync(showTimes).ConfigureAwait(false);
    }
}
