using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace FringeScraper.Services;

/// <summary>Scrapes the Fringe index page to extract show IDs.</summary>
internal static partial class IndexScraper
{
    private const string fringeIndexUrl = "https://tickets.fringetheatre.ca/events/";

    /// <summary>Scrapes all show IDs from the Fringe index page using the default fetcher.</summary>
    public static Task<List<int>> ScrapeIdsAsync()
    {
        return ScrapeIdsAsync(new Fetcher());
    }

    /// <summary>Scrapes all show IDs from the Fringe index page using the provided fetcher.</summary>
    public static async Task<List<int>> ScrapeIdsAsync(IFetcher fetcher)
    {
        IHtmlDocument document = await fetcher.LoadAsync(new Uri(fringeIndexUrl)).ConfigureAwait(false);
        IHtmlCollection<IElement> cards = document.QuerySelectorAll("div.card.text-left");

        if (cards.Length == 0)
        {
            throw new InvalidOperationException(ScraperLogger.AsString("🫠 No cards found on the page."));
        }

        List<int> ids = [];
        foreach (IElement card in cards)
        {
            IElement? anchor = card.QuerySelector(".card-footer a");
            if (anchor == null)
            {
                ScraperLogger.Log("⚠️ No anchor found in card-footer.");
                continue;
            }
            string href = anchor.GetAttribute("href") ?? "";
            Match showIdMatch = ShowIdRegex().Match(href);
            if (showIdMatch.Success)
            {
                ids.Add(int.Parse(showIdMatch.Groups[1].Value, CultureInfo.InvariantCulture));
            }
        }

        if (ids.Count == 0)
        {
            throw new InvalidOperationException(ScraperLogger.AsString("🫠 No show IDs found on the page."));
        }

        List<int> unique = [.. ids.Distinct()];
        if (unique.Count < ids.Count)
        {
            Console.WriteLine($"⚠️ Deduplicated {ids.Count - unique.Count} duplicate show IDs (page renders cards twice).");
        }

        Console.WriteLine($"🗂️ Found {unique.Count} show IDs on the page.");
        return unique;
    }

    [GeneratedRegex(@"601:(\d+)")]
    private static partial Regex ShowIdRegex();
}
