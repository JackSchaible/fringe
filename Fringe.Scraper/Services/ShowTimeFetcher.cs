using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Fringe.Data.Models;

namespace FringeScraper.Services;

/// <summary>Fetches show performance times from the Fringe ticketing site.</summary>
internal static class ShowTimeFetcher
{
    private const string baseUrl = "https://tickets.fringetheatre.ca";

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Fetches showtimes for the given show IDs using the default fetcher.</summary>
    public static Task<List<ShowTime>> PullShowtimesForShowsAsync(IEnumerable<int> showIds)
    {
        return PullShowtimesForShowsAsync(showIds, new Fetcher());
    }

    /// <summary>Fetches showtimes for the given show IDs using the provided fetcher.</summary>
    public static async Task<List<ShowTime>> PullShowtimesForShowsAsync(IEnumerable<int> showIds, IFetcher fetcher)
    {
        List<int> showIdList = [.. showIds];
        Console.WriteLine($"🗂️ Fetching showtimes for {showIdList.Count} shows.");
        ConcurrentBag<ShowTime> allShowtimes = [];
        List<int> failedIds = [];

        await Parallel.ForEachAsync(showIdList, new ParallelOptions { MaxDegreeOfParallelism = 5 }, async (showId, ct) =>
        {
            try
            {
                List<ShowTime> showtimes = await FetchShowtimesForShowAsync(showId, fetcher).ConfigureAwait(false);
                foreach (ShowTime showtime in showtimes)
                {
                    allShowtimes.Add(showtime);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ Error getting showtimes for show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                failedIds.Add(showId);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"❌ Error getting showtimes for show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                failedIds.Add(showId);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"❌ Error getting showtimes for show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                failedIds.Add(showId);
            }

            await Task.Delay(RandomNumberGenerator.GetInt32(50, 200), ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        if (failedIds.Count > 0)
        {
            Console.WriteLine($"⚠️ Failed to get showtimes for {failedIds.Count} shows: {string.Join(", ", failedIds)}");
        }

        if (allShowtimes.IsEmpty)
        {
            ScraperLogger.Log("❌ All shows errored out — no showtimes were scraped.");
        }
        else
        {
            Console.WriteLine($"✅ Successfully scraped {allShowtimes.Count} showtimes.");
        }

        return [.. allShowtimes];
    }

    private static async Task<List<ShowTime>> FetchShowtimesForShowAsync(int showId, IFetcher fetcher)
    {
        var url = new Uri($"{baseUrl}/event/601:{showId}");
        IHtmlDocument document = await fetcher.LoadAsync(url).ConfigureAwait(false);

        IElement? node = document.GetElementById("event-data");
        if (node == null)
        {
            Console.WriteLine($"⚠️ No #event-data element found on page for show ID {showId}.");
            return [];
        }

        string json = node.GetAttribute("data-performances") ?? "";
        if (string.IsNullOrWhiteSpace(json))
        {
            Console.WriteLine($"⚠️ data-performances is empty for show ID {showId}.");
            return [];
        }

        PerformancesData? data = JsonSerializer.Deserialize<PerformancesData>(json, jsonOptions);
        if (data?.Times == null || data.Times.Count == 0)
        {
            Console.WriteLine($"⚠️ No showtimes found for show ID {showId}.");
            return [];
        }

        List<ShowTime> showtimes = [];
        foreach (List<PerformanceDto> performances in data.Times.Values)
        {
            foreach (PerformanceDto p in performances)
            {
                showtimes.Add(new ShowTime
                {
                    ShowId = showId,
                    DateTime = DateTime.Parse(p.PerformanceRealTime, CultureInfo.InvariantCulture),
                    PerformanceTime = TimeOnly.Parse(p.PerformanceTime, CultureInfo.InvariantCulture),
                    PerformanceDate = p.PerformanceDate,
                    PresentationFormat = p.PresentationFormat,
                    Reserved = p.Reserved,
                });
            }
        }

        return showtimes;
    }

    private sealed class PerformancesData
    {
        /// <summary>Gets or sets the map of date strings to performance lists.</summary>
        [JsonPropertyName("times")]
        public Dictionary<string, List<PerformanceDto>>? Times { get; set; }
    }

    private sealed class PerformanceDto
    {
        /// <summary>Gets or sets the presentation format.</summary>
        [JsonPropertyName("presentationFormat")]
        public string PresentationFormat { get; set; } = "";

        /// <summary>Gets or sets the local performance time string.</summary>
        [JsonPropertyName("performanceTime")]
        public string PerformanceTime { get; set; } = "";

        /// <summary>Gets or sets the ISO-8601 performance start time.</summary>
        [JsonPropertyName("performanceRealTime")]
        public string PerformanceRealTime { get; set; } = "";

        /// <summary>Gets or sets the human-readable performance date string.</summary>
        [JsonPropertyName("performanceDate")]
        public string PerformanceDate { get; set; } = "";

        /// <summary>Gets or sets a value indicating whether seating is reserved.</summary>
        [JsonPropertyName("reserved")]
        public bool Reserved { get; set; }
    }
}
