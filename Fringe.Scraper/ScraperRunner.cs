using Fringe.Data;
using Fringe.Data.Models;
using FringeScraper.Services;

namespace FringeScraper;

/// <summary>Orchestrates the full scrape-and-insert pipeline.</summary>
internal static class ScraperRunner
{
    /// <summary>Runs the pipeline using the default HTTP fetcher, with venue enrichment skipped.</summary>
    public static Task RunAsync(FringeRepository repository)
    {
        return RunAsync(repository, new Fetcher(), geocodingProvider: null);
    }

    /// <summary>Runs the pipeline using the provided fetcher, with venue enrichment skipped.</summary>
    public static Task RunAsync(FringeRepository repository, IFetcher fetcher)
    {
        return RunAsync(repository, fetcher, geocodingProvider: null);
    }

    /// <summary>
    /// Runs the scrape-and-insert pipeline, then enriches canonical venues with coordinates via
    /// <paramref name="geocodingProvider"/>. Enrichment is skipped entirely when
    /// <paramref name="geocodingProvider"/> is <see langword="null"/> (e.g. no provider is configured).
    /// </summary>
    public static async Task RunAsync(FringeRepository repository, IFetcher fetcher, IGeocodingProvider? geocodingProvider)
    {
        ScraperLogger.Log("Beginning Fringe Scraper...");

        List<int> showIds = await IndexScraper.ScrapeIdsAsync(fetcher).ConfigureAwait(false);
        if (showIds.Count == 0)
        {
            ScraperLogger.Log("No show IDs found. Exiting.");
            return;
        }

        (List<Show> shows, List<Venue> venues, List<ContentRating> _) =
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

        FestivalImport import = new()
        {
            Shows = [.. shows],
            ShowTimes = [.. allShowTimes],
            Venues = [.. venues]
        };

        DatabaseInserter inserter = new(repository);
        await inserter.InsertDataAsync(import).ConfigureAwait(false);

        if (geocodingProvider != null)
        {
            VenueEnrichmentService enrichment = new(repository, geocodingProvider);
            await enrichment.EnrichAsync().ConfigureAwait(false);
        }
        else
        {
            ScraperLogger.Log("No geocoding provider configured — skipping venue enrichment.");
        }

        ScraperLogger.Log("Scraping and insertion complete.");
    }
}
