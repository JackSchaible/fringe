namespace FringeScraper.Services;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using AngleSharp.Html.Dom;
using Models;

public static class ShowTimeFetcher
{
    private const string BaseUrl = "https://tickets.fringetheatre.ca";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<List<ShowTime>> PullShowtimesForShowsAsync(List<int> showIds)
    {
        Console.WriteLine($"🗂️ Fetching showtimes for {showIds.Count} shows.");
        ConcurrentBag<ShowTime> allShowtimes = [];
        List<int> failedIds = [];

        await Parallel.ForEachAsync(showIds, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (showId, ct) =>
        {
            try
            {
                List<ShowTime> showtimes = await FetchShowtimesForShowAsync(showId);
                foreach (ShowTime showtime in showtimes)
                    allShowtimes.Add(showtime);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting showtimes for show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                failedIds.Add(showId);
            }

            await Task.Delay(Random.Shared.Next(50, 200), ct);
        });

        if (failedIds.Count > 0)
            Console.WriteLine($"⚠️ Failed to get showtimes for {failedIds.Count} shows: {string.Join(", ", failedIds)}");

        if (allShowtimes.Count == 0)
            Console.WriteLine("❌ All shows errored out — no showtimes were scraped.");
        else
            Console.WriteLine($"✅ Successfully scraped {allShowtimes.Count} showtimes.");

        return allShowtimes.ToList();
    }

    private static async Task<List<ShowTime>> FetchShowtimesForShowAsync(int showId)
    {
        string url = $"{BaseUrl}/event/601:{showId}";
        IHtmlDocument document = await Fetcher.LoadAsync(url);

        var node = document.GetElementById("event-data");
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

        PerformancesData? data = JsonSerializer.Deserialize<PerformancesData>(json, JsonOptions);
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
                    DateTime = DateTime.Parse(p.PerformanceRealTime),
                    PerformanceTime = TimeOnly.Parse(p.PerformanceTime),
                    PerformanceDate = p.PerformanceDate,
                    PresentationFormat = p.PresentationFormat,
                    Reserved = p.Reserved,
                });
            }
        }

        return showtimes;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private class PerformancesData
    {
        [JsonPropertyName("times")]
        public Dictionary<string, List<PerformanceDto>>? Times { get; set; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private class PerformanceDto
    {
        [JsonPropertyName("presentationFormat")]
        public string PresentationFormat { get; set; } = "";
        [JsonPropertyName("performanceTime")]
        public string PerformanceTime { get; set; } = "";
        [JsonPropertyName("performanceRealTime")]
        public string PerformanceRealTime { get; set; } = "";
        [JsonPropertyName("performanceDate")]
        public string PerformanceDate { get; set; } = "";
        [JsonPropertyName("reserved")]
        public bool Reserved { get; set; }
    }
}
