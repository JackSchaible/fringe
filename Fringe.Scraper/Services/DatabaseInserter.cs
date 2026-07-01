using Fringe.Data;
using FringeScraper.Models;

namespace FringeScraper.Services;

public class DatabaseInserter(FringeRepository repository)
{
    public async Task InsertDataAsync(List<Show> shows, List<ShowTime> showTimes)
    {
        Console.WriteLine("Inserting shows into DynamoDB...");
        await repository.SaveShowsAsync(shows);

        Console.WriteLine("Inserting showtimes into DynamoDB...");
        await repository.SaveShowTimesAsync(showTimes);
    }
}
