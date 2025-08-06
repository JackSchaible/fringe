namespace FringeScraper.Services;

using System.Text.RegularExpressions;
using HtmlAgilityPack;

public static partial class IndexScraper
{
    private const string FringeIndexUrl = "https://tickets.fringetheatre.ca/events/";
    
    public static async Task<List<int>> ScrapeIdsAsync()
    {
        HtmlWeb web = new();
        HtmlDocument document = await web.LoadFromWebAsync(FringeIndexUrl);
        HtmlNodeCollection cards = document.DocumentNode.SelectNodes("//div[contains(@class, 'card') and contains(@class, 'text-left')]");

        if (cards == null)
            throw new Exception("🫠 No cards found on the page.");

        List<int> ids = [];
        foreach (HtmlNode card in cards)
        {
            string href = card.SelectSingleNode(".//div[contains(@class, 'card-footer')]//a").Attributes["href"].Value;
            Match showIdMatch = ShowIdRegex().Match(href);
            int showId = showIdMatch.Success ? int.Parse(showIdMatch.Groups[1].Value) : 0;
            if (string.IsNullOrWhiteSpace(href))
            {
                Console.WriteLine("⚠️ No href found in card.");
                continue;
            }
            
            ids.Add(showId);
        }
        
        if (ids.Count == 0)
            throw new Exception("🫠 No show IDs found on the page.");
        
        Console.WriteLine($"🗂️ Found {ids.Count} show IDs on the page.");
        return ids;
    }

    [GeneratedRegex(@"601:(\d+)")]
    private static partial Regex ShowIdRegex();
}