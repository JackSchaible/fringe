namespace FringeScraper.Services;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
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
                Console.WriteLine($"❌ Error scraping show ID {showId}: {ex.Message}");
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
        HtmlWeb web = new();
        HtmlDocument document = await web.LoadFromWebAsync(url);
        
        string? title = document.DocumentNode.SelectSingleNode("//div[contains(@class, 'content')]//h2")?.InnerText.Trim() ?? "";
        HtmlNodeCollection? descriptionHtml = document.DocumentNode.SelectNodes("//div[contains(@class, 'content')]//p");
        string plainTextDescription = string.Join("\n\n", descriptionHtml.Select(p => HtmlEntity.DeEntitize(p.InnerText.Trim())));

        string? tag = document.DocumentNode.SelectSingleNode("//ul[contains(@class, 'schedule')]/li[1]").InnerText.Trim() ?? "";

        (decimal price, decimal fee) = ExtractPriceAndFee(document);
        DateOnly firstShowDate = ExtractFirstShowDate(document);
        int length = ExtractDuration(document);
        Venue venue = ExtractVenue(document);
        ContentRating contentRating = ExtractContentRating(document);
        string description = ExtractOriginalDescription(document);

        string? img = document.DocumentNode.SelectSingleNode("//img[contains(@class, 'event-image-square')]").GetAttributeValue("src", "");
        
        Show show = new Show
        {
            Id = showId,
            Title = HtmlEntity.DeEntitize(title),
            PlainTextDescription = plainTextDescription,
            Description = description,
            Tag = HtmlEntity.DeEntitize(tag),
            ImageUrl = img ?? "",
            Price = price,
            Fee = fee,
            FirstShowDate = firstShowDate,
            LengthInMinutes = length,
            Venue = venue,
            ContentRating = contentRating,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        return show;
    }
    
     private static (decimal Price, decimal Fee) ExtractPriceAndFee(HtmlDocument doc)
    {
        HtmlNode? priceNode = doc.DocumentNode.SelectSingleNode("//ul[contains(@class, 'schedule')]/li[contains(text(),'inc')]");
        string text = priceNode?.InnerText ?? "";
        Match match = PriceRegex().Match(text);

        if (!match.Success) return (0, 0);
        
        decimal total = decimal.Parse(match.Groups[1].Value);
        decimal fee = decimal.Parse(match.Groups[2].Value);
        return (total - fee, fee);
    }

    private static DateOnly ExtractFirstShowDate(HtmlDocument doc)
    {
        string? dateText = doc.DocumentNode
            .SelectSingleNode("//ul[contains(@class, 'schedule')]/li[contains(text(),'August')]")
            ?.InnerText.Trim();

        if (dateText == null)
            return DateOnly.MinValue;

        // Example: "14-August 24, 2025" → we take 14th
        Match match = DateRegex().Match(dateText);
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out int day) &&
            int.TryParse(match.Groups[2].Value, out int year))
        {
            return new DateOnly(year, 8, day); // August is month 8
        }

        return DateOnly.MinValue;
    }

    private static int ExtractDuration(HtmlDocument doc)
    {
        string? durText = doc.DocumentNode
            .SelectSingleNode("//ul[contains(@class, 'schedule')]/li[contains(text(),'minute')]")
            ?.InnerText.Trim();

        Match match = DurationRegex().Match(durText ?? "");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static Venue ExtractVenue(HtmlDocument doc)
    {
        HtmlNode? venueSection = doc.DocumentNode.SelectSingleNode("//section[contains(@class, 'venu-main')]");

        // Grab the <h3> with the "##: Venue Name" format
        HtmlNode? numberLine = venueSection?.SelectNodes(".//h3")?.FirstOrDefault(h =>
            VenueIdAndNameRegex().IsMatch(h.InnerText.Trim()));

        int venueNumber = -1;
        string venueName = "Unknown";

        if (numberLine != null)
        {
            Match match = VenueIdRegex().Match(numberLine.InnerText.Trim());
            if (match.Success)
            {
                venueNumber = int.Parse(match.Groups["num"].Value);
                venueName = HtmlEntity.DeEntitize(match.Groups["name"].Value.Trim());
            }
        }

        // Address = first <p>
        string address = venueSection?.SelectSingleNode(".//p[1]")?.InnerText.Trim() ?? "";
        string postal = venueSection?.SelectSingleNode(".//p[2]")?.InnerText.Trim().Replace(" ", "") ?? "";
        string phone = venueSection?.SelectSingleNode(".//span")?.InnerText.Trim().Replace("-", "").Replace(" ", "") ?? "";

        return new Venue
        {
            VenueNumber = venueNumber,
            Name = venueName,
            Address = address,
            PostalCode = postal,
            Phone = phone,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static ContentRating ExtractContentRating(HtmlDocument doc)
    {
        string? ratingText = doc.DocumentNode
            .SelectSingleNode("//ul[contains(@class, 'schedule')]/li[contains(text(),'(')]")
            ?.InnerText.Trim();

        Match match = RatingRegex().Match(ratingText ?? "");
        if (match.Success)
        {
            return new ContentRating
            {
                Name = HtmlEntity.DeEntitize(match.Groups[1].Value.Trim()),
                Code = match.Groups[2].Value.Trim(),
                Description = ""
            };
        }

        return new ContentRating
        {
            Name = "Unrated",
            Code = "UR",
            Description = ""
        };
    }
    
    private static string ExtractOriginalDescription(HtmlDocument doc)
    {
        HtmlNode h2 = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'content')]//h2");

        // Grab all <p> siblings after the title <h2>, but before <ul class="schedule">
        HtmlNode contentNode = h2.ParentNode;
        List<string> paragraphs = [];

        foreach (HtmlNode node in contentNode.ChildNodes)
        {
            if (node.Name == "ul" && node.GetAttributeValue("class", "").Contains("schedule"))
                break;

            if (node.Name != "p") continue;
            
            string text = HtmlEntity.DeEntitize(node.InnerText.Trim());
            if (!string.IsNullOrWhiteSpace(text))
                paragraphs.Add(text);
        }
        
        return string.Join("\n\n", paragraphs);
    }

    [GeneratedRegex(@"\$(\d+(?:\.\d+)?)\s+inc\s+\$(\d+(?:\.\d+)?)")]
    private static partial Regex PriceRegex();
    [GeneratedRegex(@"(\d{1,2})-(?:\w+)\s+\d{1,2},\s*(\d{4})")]
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