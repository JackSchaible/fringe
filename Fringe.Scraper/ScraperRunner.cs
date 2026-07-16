using Fringe.Data;
using Fringe.Data.Models;
using FringeScraper.Services;

namespace FringeScraper;

/// <summary>Orchestrates the full scrape-and-insert pipeline.</summary>
internal static class ScraperRunner
{
    /// <summary>Runs the pipeline using the default HTTP fetcher.</summary>
    public static Task RunAsync(FringeRepository repository)
    {
        return RunAsync(repository, new Fetcher());
    }

    /// <summary>Runs the pipeline using the provided fetcher.</summary>
    public static async Task RunAsync(FringeRepository repository, IFetcher fetcher)
    {
        ScraperLogger.Log("Beginning Fringe Scraper...");

        List<int> showIds = await IndexScraper.ScrapeIdsAsync(fetcher).ConfigureAwait(false);
        if (showIds.Count == 0)
        {
            ScraperLogger.Log("No show IDs found. Exiting.");
            return;
        }

        (List<Show> shows, List<Venue> _, List<ContentRating> _) =
            await DetailScraper.ScrapeShowsAsync(showIds, fetcher).ConfigureAwait(false);
        if (shows.Count == 0)
        {
            ScraperLogger.Log("No shows found. Exiting.");
            return;
        }

        List<ShowTime> allShowTimes =
            await ShowTimeFetcher.PullShowtimesForShowsAsync(showIds, fetcher).ConfigureAwait(false);
        if (allShowTimes.Count == 0)
        {
            ScraperLogger.Log("No showtimes found. Exiting.");
            return;
        }

        DatabaseInserter inserter = new(repository);
        await inserter.InsertDataAsync(shows, allShowTimes).ConfigureAwait(false);

        ScraperLogger.Log("Scraping and insertion complete.");
    }
}
