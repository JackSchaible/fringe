using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Fringe.Data.Models;

namespace FringeScraper.Services;

/// <summary>Scrapes detailed show metadata from individual Fringe show pages.</summary>
internal static partial class DetailScraper
{
    private const string baseUrl = "https://tickets.fringetheatre.ca";

    /// <summary>Scrapes show details for the given show IDs using the default fetcher.</summary>
    public static Task<(List<Show>, List<Venue>, List<ContentRating>)> ScrapeShowsAsync(IEnumerable<int> showIds)
    {
        return ScrapeShowsAsync(showIds, new Fetcher());
    }

    /// <summary>Scrapes show details for the given show IDs using the provided fetcher.</summary>
    public static async Task<(List<Show>, List<Venue>, List<ContentRating>)> ScrapeShowsAsync(IEnumerable<int> showIds, IFetcher fetcher)
    {
        ConcurrentBag<Show> shows = [];
        List<int> failedIds = [];

        await Parallel.ForEachAsync(showIds, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (showId, ct) =>
        {
            try
            {
                Show? show = await ScrapeShowDetailsAsync(showId, fetcher).ConfigureAwait(false);
                if (show != null)
                {
                    shows.Add(show);
                }
                else
                {
                    failedIds.Add(showId);
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ Error scraping show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"   at {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                failedIds.Add(showId);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"❌ Error scraping show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"   at {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                failedIds.Add(showId);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"❌ Error scraping show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"   at {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                failedIds.Add(showId);
            }
            catch (OverflowException ex)
            {
                Console.WriteLine($"❌ Error scraping show ID {showId}: {ex.GetType().Name}: {ex.Message}");
                Console.WriteLine($"   at {ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                failedIds.Add(showId);
            }

            await Task.Delay(RandomNumberGenerator.GetInt32(50, 200), ct).ConfigureAwait(false);
        }).ConfigureAwait(false);

        if (failedIds.Count > 0)
        {
            Console.WriteLine($"⚠️ Failed to scrape {failedIds.Count} shows: {string.Join(", ", failedIds)}");
        }

        Console.WriteLine($"✅ Successfully scraped {shows.Count} shows.");

        List<Venue> dedupedVenues = [.. shows.Select(s => s.Venue).GroupBy(v => v.VenueNumber).Select(g => g.First())];
        List<ContentRating> dedupedRatings = [.. shows.Select(s => s.ContentRating).GroupBy(r => r.Code).Select(g => g.First())];

        var venueDict = dedupedVenues.ToDictionary(v => v.VenueNumber);
        var ratingDict = dedupedRatings.ToDictionary(r => r.Code);

        foreach (Show show in shows)
        {
            if (venueDict.TryGetValue(show.Venue.VenueNumber, out Venue? venue))
            {
                show.Venue = venue;
            }
            else
            {
                Console.WriteLine($"⚠️ Venue {show.Venue.VenueNumber} not found for show {show.Id}. Using default.");
            }

            if (ratingDict.TryGetValue(show.ContentRating.Code, out ContentRating? rating))
            {
                show.ContentRating = rating;
            }
            else
            {
                Console.WriteLine($"⚠️ Rating {show.ContentRating.Code} not found for show {show.Id}. Using default.");
            }
        }

        return ([.. shows], dedupedVenues, dedupedRatings);
    }

    private static async Task<Show?> ScrapeShowDetailsAsync(int showId, IFetcher fetcher)
    {
        var url = new Uri($"{baseUrl}/event/601:{showId}");
        IHtmlDocument document = await fetcher.LoadAsync(url).ConfigureAwait(false);

        string title = document.QuerySelector(".content h2")?.TextContent.Trim() ?? "";
        IHtmlCollection<IElement> descParagraphs = document.QuerySelectorAll(".content p");
        string plainTextDescription = string.Join("\n\n",
            descParagraphs.Select(p => p.TextContent.Trim()).Where(t => !string.IsNullOrWhiteSpace(t)));

        string tag = document.QuerySelector("ul.schedule li:first-of-type")?.TextContent.Trim() ?? "";

        (decimal price, decimal fee) = ExtractPriceAndFee(document);
        DateOnly firstShowDate = ExtractFirstShowDate(document);
        int length = ExtractDuration(document);
        Venue venue = ExtractVenue(document);
        ContentRating contentRating = ExtractContentRating(document);
        string description = ExtractOriginalDescription(document);

        string imgSrc = document.QuerySelector("img.event-image-square")?.GetAttribute("src") ?? "";
        Uri? imageUrl = Uri.TryCreate(imgSrc, UriKind.Absolute, out Uri? parsedUri) ? parsedUri : null;

        return new Show
        {
            Id = showId,
            Title = title,
            PlainTextDescription = plainTextDescription,
            Description = description,
            Tag = tag,
            ImageUrl = imageUrl,
            Price = price,
            Fee = fee,
            FirstShowDate = firstShowDate,
            LengthInMinutes = length,
            Venue = venue,
            ContentRating = contentRating,
        };
    }

    private static IEnumerable<string> ScheduleItemTexts(IHtmlDocument doc)
    {
        return doc.QuerySelectorAll("ul.schedule li").Select(li => li.TextContent.Trim());
    }

    private static (decimal Price, decimal Fee) ExtractPriceAndFee(IHtmlDocument doc)
    {
        string text = ScheduleItemTexts(doc).FirstOrDefault(t => t.Contains("inc", StringComparison.Ordinal)) ?? "";
        Match match = PriceRegex().Match(text);
        if (!match.Success)
        {
            return (0, 0);
        }

        decimal total = decimal.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        decimal fee = decimal.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return (total - fee, fee);
    }

    private static DateOnly ExtractFirstShowDate(IHtmlDocument doc)
    {
        string? dateText = ScheduleItemTexts(doc).FirstOrDefault(t => DateRegex().IsMatch(t));
        if (dateText == null)
        {
            return DateOnly.MinValue;
        }

        Match match = DateRegex().Match(dateText);
        return match.Success
            && int.TryParse(match.Groups["day"].Value, out int day)
            && int.TryParse(match.Groups["year"].Value, out int year)
            && DateTime.TryParseExact(match.Groups["month"].Value, "MMMM",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime monthDate)
            ? new DateOnly(year, monthDate.Month, day)
            : DateOnly.MinValue;
    }

    private static int ExtractDuration(IHtmlDocument doc)
    {
        string? durText = ScheduleItemTexts(doc).FirstOrDefault(t => t.Contains("minute", StringComparison.Ordinal));
        Match match = DurationRegex().Match(durText ?? "");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static Venue ExtractVenue(IHtmlDocument doc)
    {
        IElement? venueSection = doc.QuerySelector("section.venu-main");

        int venueNumber = -1;
        string venueName = "Unknown";

        if (venueSection != null)
        {
            IElement? h3 = venueSection.QuerySelectorAll("h3")
                .FirstOrDefault(h => VenueIdAndNameRegex().IsMatch(h.TextContent.Trim()));

            if (h3 != null)
            {
                Match match = VenueIdRegex().Match(h3.TextContent.Trim());
                if (match.Success)
                {
                    venueNumber = int.Parse(match.Groups["num"].Value, CultureInfo.InvariantCulture);
                    venueName = match.Groups["name"].Value.Trim();
                }
            }
        }

        List<IElement> paras = venueSection != null ? [.. venueSection.QuerySelectorAll("p")] : [];
        string address = paras.Count > 0 ? paras[0].TextContent.Trim() : "";
        string postal = paras.Count > 1 ? paras[1].TextContent.Trim().Replace(" ", "", StringComparison.Ordinal) : "";
        string phone = venueSection?.QuerySelector("span")?.TextContent.Trim()
            .Replace("-", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal) ?? "";

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
        string? ratingText = ScheduleItemTexts(doc).FirstOrDefault(t => t.Contains('(', StringComparison.Ordinal));

        Match match = RatingRegex().Match(ratingText ?? "");
        return match.Success
            ? new ContentRating { Name = match.Groups[1].Value.Trim(), Code = match.Groups[2].Value.Trim(), Description = "" }
            : new ContentRating { Name = "Unrated", Code = "UR", Description = "" };
    }

    private static string ExtractOriginalDescription(IHtmlDocument doc)
    {
        IElement? h2 = doc.QuerySelector(".content h2");
        if (h2?.ParentElement == null)
        {
            return "";
        }

        List<string> paragraphs = [];
        foreach (INode node in h2.ParentElement.ChildNodes)
        {
            if (node is not IElement el)
            {
                continue;
            }
            if (el.LocalName == "ul" && el.ClassList.Contains("schedule"))
            {
                break;
            }
            if (el.LocalName != "p")
            {
                continue;
            }

            string text = el.TextContent.Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                paragraphs.Add(text);
            }
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
