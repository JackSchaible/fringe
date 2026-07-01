namespace FringeScraper.Services;

using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;

public static partial class IndexScraper
{
    private const string FringeIndexUrl = "https://tickets.fringetheatre.ca/events/";

    public static async Task<List<int>> ScrapeIdsAsync()
    {
        IHtmlDocument document = await Fetcher.LoadAsync(FringeIndexUrl);
        var cards = document.QuerySelectorAll("div.card.text-left");

        if (!cards.Any())
            throw new Exception("🫠 No cards found on the page.");

        List<int> ids = [];
        foreach (var card in cards)
        {
            var anchor = card.QuerySelector(".card-footer a");
            if (anchor == null)
            {
                Console.WriteLine("⚠️ No anchor found in card-footer.");
                continue;
            }
            string href = anchor.GetAttribute("href") ?? "";
            Match showIdMatch = ShowIdRegex().Match(href);
            if (showIdMatch.Success)
                ids.Add(int.Parse(showIdMatch.Groups[1].Value));
        }

        if (ids.Count == 0)
            throw new Exception("🫠 No show IDs found on the page.");

        List<int> unique = ids.Distinct().ToList();
        if (unique.Count < ids.Count)
            Console.WriteLine($"⚠️ Deduplicated {ids.Count - unique.Count} duplicate show IDs (page renders cards twice).");

        Console.WriteLine($"🗂️ Found {unique.Count} show IDs on the page.");
        return unique;
    }

    [GeneratedRegex(@"601:(\d+)")]
    private static partial Regex ShowIdRegex();
}
