namespace FringeScraper.Services;

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Models;

public static partial class DetailScraper
{
    private const string BaseUrl = "https://tickets.fringetheatre.ca";

    public static async Task<(List<Show>, List<Venue>, List<ContentRating>)> ScrapeShowsAsync(List<int> showIds)
    {
        ConcurrentBag<Show> shows = [];
        List<int> failedIds = [];

        await Parallel.ForEachAsync(showIds, new ParallelOptions { MaxDegreeOfParallelism = 25 }, async (showId, ct) =>
        {
            try
            {
                Show? show = await ScrapeShowDetailsAsync(showId);
                if (show != null)
                    shows.Add(show);
                else
                    failedIds.Add(showId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error scraping show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"   at {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                failedIds.Add(showId);
            }

            await Task.Delay(Random.Shared.Next(50, 200), ct);
        });

        if (failedIds.Count > 0)
            Console.WriteLine($"⚠️ Failed to scrape {failedIds.Count} shows: {string.Join(", ", failedIds)}");

        Console.WriteLine($"✅ Successfully scraped {shows.Count} shows.");

        List<Venue> dedupedVenues = shows
            .Select(s => s.Venue)
            .GroupBy(v => v.VenueNumber)
            .Select(g => g.First())
            .ToList();

        List<ContentRating> dedupedRatings = shows
            .Select(s => s.ContentRating)
            .GroupBy(r => r.Code)
            .Select(g => g.First())
            .ToList();

        Dictionary<int, Venue> venueDict = dedupedVenues.ToDictionary(v => v.VenueNumber);
        Dictionary<string, ContentRating> ratingDict = dedupedRatings.ToDictionary(r => r.Code);

        foreach (Show show in shows)
        {
            if (venueDict.TryGetValue(show.Venue.VenueNumber, out Venue? venue))
                show.Venue = venue;
            else
                Console.WriteLine($"⚠️ Venue {show.Venue.VenueNumber} not found for show {show.Id}. Using default.");

            if (ratingDict.TryGetValue(show.ContentRating.Code, out ContentRating? rating))
                show.ContentRating = rating;
            else
                Console.WriteLine($"⚠️ Rating {show.ContentRating.Code} not found for show {show.Id}. Using default.");
        }

        return (shows.ToList(), dedupedVenues, dedupedRatings);
    }

    private static async Task<Show?> ScrapeShowDetailsAsync(int showId)
    {
        string url = $"{BaseUrl}/event/601:{showId}";
        IHtmlDocument document = await Fetcher.LoadAsync(url);

        string title = document.QuerySelector(".content h2")?.TextContent.Trim() ?? "";
        var descParagraphs = document.QuerySelectorAll(".content p");
        string plainTextDescription = string.Join("\n\n",
            descParagraphs.Select(p => p.TextContent.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)));

        string tag = document.QuerySelector("ul.schedule li:first-of-type")?.TextContent.Trim() ?? "";

        (decimal price, decimal fee) = ExtractPriceAndFee(document);
        DateOnly firstShowDate = ExtractFirstShowDate(document);
        int length = ExtractDuration(document);
        Venue venue = ExtractVenue(document);
        ContentRating contentRating = ExtractContentRating(document);
        string description = ExtractOriginalDescription(document);

        string img = document.QuerySelector("img.event-image-square")?.GetAttribute("src") ?? "";

        return new Show
        {
            Id = showId,
            Title = title,
            PlainTextDescription = plainTextDescription,
            Description = description,
            Tag = tag,
            ImageUrl = img,
            Price = price,
            Fee = fee,
            FirstShowDate = firstShowDate,
            LengthInMinutes = length,
            Venue = venue,
            ContentRating = contentRating,
        };
    }

    private static IEnumerable<string> ScheduleItemTexts(IHtmlDocument doc) =>
        doc.QuerySelectorAll("ul.schedule li").Select(li => li.TextContent.Trim());

    private static (decimal Price, decimal Fee) ExtractPriceAndFee(IHtmlDocument doc)
    {
        string text = ScheduleItemTexts(doc).FirstOrDefault(t => t.Contains("inc")) ?? "";
        Match match = PriceRegex().Match(text);
        if (!match.Success) return (0, 0);

        decimal total = decimal.Parse(match.Groups[1].Value);
        decimal fee = decimal.Parse(match.Groups[2].Value);
        return (total - fee, fee);
    }

    private static DateOnly ExtractFirstShowDate(IHtmlDocument doc)
    {
        string? dateText = ScheduleItemTexts(doc).FirstOrDefault(t => DateRegex().IsMatch(t));
        if (dateText == null) return DateOnly.MinValue;

        // Format: "9-July 12, 2026" → start day, month name, year
        Match match = DateRegex().Match(dateText);
        if (!match.Success) return DateOnly.MinValue;

        if (!int.TryParse(match.Groups["day"].Value, out int day)) return DateOnly.MinValue;
        if (!int.TryParse(match.Groups["year"].Value, out int year)) return DateOnly.MinValue;
        if (!DateTime.TryParseExact(match.Groups["month"].Value, "MMMM",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime monthDate))
            return DateOnly.MinValue;

        return new DateOnly(year, monthDate.Month, day);
    }

    private static int ExtractDuration(IHtmlDocument doc)
    {
        string? durText = ScheduleItemTexts(doc).FirstOrDefault(t => t.Contains("minute"));
        Match match = DurationRegex().Match(durText ?? "");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static Venue ExtractVenue(IHtmlDocument doc)
    {
        var venueSection = doc.QuerySelector("section.venu-main");

        int venueNumber = -1;
        string venueName = "Unknown";

        if (venueSection != null)
        {
            var h3 = venueSection.QuerySelectorAll("h3")
                .FirstOrDefault(h => VenueIdAndNameRegex().IsMatch(h.TextContent.Trim()));

            if (h3 != null)
            {
                Match match = VenueIdRegex().Match(h3.TextContent.Trim());
                if (match.Success)
                {
                    venueNumber = int.Parse(match.Groups["num"].Value);
                    venueName = match.Groups["name"].Value.Trim();
                }
            }
        }

        var paras = venueSection?.QuerySelectorAll("p").ToList() ?? [];
        string address = paras.Count > 0 ? paras[0].TextContent.Trim() : "";
        string postal = paras.Count > 1 ? paras[1].TextContent.Trim().Replace(" ", "") : "";
        string phone = venueSection?.QuerySelector("span")?.TextContent.Trim()
            .Replace("-", "").Replace(" ", "") ?? "";

        return new Venue
        {
            VenueNumber = venueNumber,
            Name = venueName,
            Address = address,
            PostalCode = postal,
            Phone = phone,
        };
    }

    private static ContentRating ExtractContentRating(IHtmlDocument doc)
    {
        string? ratingText = ScheduleItemTexts(doc).FirstOrDefault(t => t.Contains("("));

        Match match = RatingRegex().Match(ratingText ?? "");
        if (match.Success)
        {
            return new ContentRating
            {
                Name = match.Groups[1].Value.Trim(),
                Code = match.Groups[2].Value.Trim(),
                Description = ""
            };
        }

        return new ContentRating { Name = "Unrated", Code = "UR", Description = "" };
    }

    private static string ExtractOriginalDescription(IHtmlDocument doc)
    {
        var h2 = doc.QuerySelector(".content h2");
        if (h2?.ParentElement == null) return "";

        List<string> paragraphs = [];
        foreach (INode node in h2.ParentElement.ChildNodes)
        {
            if (node is not IElement el) continue;
            if (el.LocalName == "ul" && el.ClassList.Contains("schedule")) break;
            if (el.LocalName != "p") continue;

            string text = el.TextContent.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                paragraphs.Add(text);
        }

        return string.Join("\n\n", paragraphs);
    }

    [GeneratedRegex(@"\$(\d+(?:\.\d+)?)\s+inc\s+\$(\d+(?:\.\d+)?)")]
    private static partial Regex PriceRegex();
    [GeneratedRegex(@"(?<day>\d{1,2})-(?<month>[A-Za-z]+)\s+\d{1,2},\s*(?<year>\d{4})")]
    private static partial Regex DateRegex();
    [GeneratedRegex(@"(\d+)")]
    private static partial Regex DurationRegex();
    [GeneratedRegex(@"^\d{2}:\s")]
    private static partial Regex VenueIdAndNameRegex();
    [GeneratedRegex(@"^(?<num>\d{2}):\s(?<name>.+)")]
    private static partial Regex VenueIdRegex();
    [GeneratedRegex(@"(.+?)\s+\((\w+)\)")]
    private static partial Regex RatingRegex();
}
