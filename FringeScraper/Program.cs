using FringeScraper.Models;
using FringeScraper.Services;
using Microsoft.Extensions.Configuration;

IConfigurationRoot config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appSettings.json", optional: true, reloadOnChange: true)
    .Build();

string? connectionString = config.GetConnectionString("dbCxnString");

Console.WriteLine("🏹 Beginning Fringe Scraper...");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.WriteLine("❗ Connection string is not set in appSettings.json.");
    return;
}

List<int> showIds = await IndexScraper.ScrapeIdsAsync();

if (showIds.Count == 0)
{
    Console.WriteLine("❗ No show IDs found. Exiting.");
    return;
}

(List<Show>, List<Venue>, List<ContentRating>) detailScrapeResults = await DetailScraper.ScrapeShowsAsync(showIds);

if (detailScrapeResults.Item1.Count == 0)
{
    Console.WriteLine("❗ No shows found. Exiting.");
    return;
}

List<ShowTime> allShowTimes = await ShowTimeFetcher.PullShowtimesForShowsAsync(showIds);
if (allShowTimes.Count == 0)
{
    Console.WriteLine("❗ No showtimes found. Exiting.");
    return;
}

DatabaseInserter inserter = new(connectionString);
await inserter.InsertDataAsync(
    detailScrapeResults.Item1, 
    detailScrapeResults.Item2, 
    detailScrapeResults.Item3, 
    allShowTimes);

Console.WriteLine("✅ Scraping and database insertion complete.");


