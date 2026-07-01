using Fringe.Data;
using FringeScraper.Models;
using FringeScraper.Services;

namespace FringeScraper;

public static class ScraperRunner
{
    public static async Task RunAsync(FringeRepository repository)
    {
        Console.WriteLine("Beginning Fringe Scraper...");

        List<int> showIds = await IndexScraper.ScrapeIdsAsync();
        if (showIds.Count == 0)
        {
            Console.WriteLine("No show IDs found. Exiting.");
            return;
        }

        (List<Show> shows, List<Venue> _, List<ContentRating> _) = await DetailScraper.ScrapeShowsAsync(showIds);
        if (shows.Count == 0)
        {
            Console.WriteLine("No shows found. Exiting.");
            return;
        }

        List<ShowTime> allShowTimes = await ShowTimeFetcher.PullShowtimesForShowsAsync(showIds);
        if (allShowTimes.Count == 0)
        {
            Console.WriteLine("No showtimes found. Exiting.");
            return;
        }

        DatabaseInserter inserter = new(repository);
        await inserter.InsertDataAsync(shows, allShowTimes);

        Console.WriteLine("Scraping and insertion complete.");
    }
}
