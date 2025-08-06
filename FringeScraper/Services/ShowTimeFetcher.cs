namespace FringeScraper.Services;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Models;

public static class ShowTimeFetcher
{
    private const string AjaxUrl = "https://tickets.fringetheatre.ca/wp-admin/admin-ajax.php";

    public static async Task<List<ShowTime>> PullShowtimesForShowsAsync(List<int> showIds)
    {
        Console.WriteLine($"🗂️ Found {showIds.Count} show IDs to pull showtimes for.");
        ConcurrentBag<ShowTime> allShowtimes = [];
        List<int> failedIds = [];
        
        await Parallel.ForEachAsync(showIds, new ParallelOptions { MaxDegreeOfParallelism = 25 }, async (showId, ct) =>
        {
            try
            {
                List<ShowTime> showtimes = await FetchShowtimesForShowAsync(showId);
                foreach (ShowTime showtime in showtimes)
                {
                    allShowtimes.Add(showtime);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting showtimes for show ID {showId}: {ex.Message}");
                failedIds.Add(showId);
            }
            
            await Task.Delay(Random.Shared.Next(50, 200), ct);
        });

        if (failedIds.Count > 0)
            Console.WriteLine($"⚠️ Failed to get {failedIds.Count} showtimes: {string.Join(", ", failedIds)}");
        
        Console.WriteLine($"✅ Successfully scraped {allShowtimes.Count} showtimes.");
        
        return allShowtimes.ToList();
    }
    
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri(AjaxUrl)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static async Task<List<ShowTime>> FetchShowtimesForShowAsync(int showId)
    {
        FormUrlEncodedContent content = new([
            new KeyValuePair<string, string>("action", "GetShowtimesForEvent"),
            new KeyValuePair<string, string>("selectedDateFormattedForSoap", "2025-08-11T00:00:00"),
            new KeyValuePair<string, string>("selectedDateNextDayFormattedForSoap", "2025-08-24T23:59:59"),
            new KeyValuePair<string, string>("eventId", $"601:{showId}")
        ]);

        HttpResponseMessage response = await Http.PostAsync(AjaxUrl, content);
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"❗ Failed to fetch showtimes for show ID {showId}: {response.ReasonPhrase}");
            return [];
        }

        string body = await response.Content.ReadAsStringAsync();

        try
        {
            List<ShowTimeDto>? rawShowtimes = JsonSerializer.Deserialize<List<ShowTimeDto>>(body, JsonOptions);
            if (rawShowtimes != null && rawShowtimes.Count != 0)
            {
                return rawShowtimes.Select(dto => new ShowTime
                {
                    ShowId = showId,
                    DateTime = DateTime.Parse(dto.datetime),
                    PerformanceTime = TimeOnly.Parse(dto.performanceTime),
                    PerformanceDate = dto.performanceDate,
                    PresentationFormat = dto.presentationFormat,
                    Reserved = dto.reserved,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }).ToList();
            }

            Console.WriteLine($"⚠️ No showtimes found for show ID {showId}.");
            return [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❗ Error parsing showtimes for show ID {showId}: {ex.Message}");
            return [];
        }
    }
    
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    // ReSharper disable once ClassNeverInstantiated.Local
    private class ShowTimeDto
    {
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public required string id;
        public string? title;
        public required string datetime;
        public required string performanceTime;
        public required string performanceDate;
        public required string presentationFormat;
        public bool reserved;
#pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
    }
}