using Fringe.Data;
using Fringe.Data.Models;

namespace FringeScraper.Services;

/// <summary>Inserts a normalized festival import into DynamoDB via <see cref="FringeRepository"/>.</summary>
internal sealed class DatabaseInserter(FringeRepository repository)
{
    /// <summary>Saves venues, shows, and showtimes to DynamoDB.</summary>
    public async Task InsertDataAsync(FestivalImport import)
    {
        ScraperLogger.Log("Inserting venues into DynamoDB...");
        await repository.SaveVenuesAsync(import.Venues).ConfigureAwait(false);

        ScraperLogger.Log("Inserting shows into DynamoDB...");
        await repository.SaveShowsAsync(import.Shows).ConfigureAwait(false);

        ScraperLogger.Log("Inserting showtimes into DynamoDB...");
        await repository.SaveShowTimesAsync(import.ShowTimes).ConfigureAwait(false);
    }
}
