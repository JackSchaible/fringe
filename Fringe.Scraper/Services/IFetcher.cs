using AngleSharp.Html.Dom;

namespace FringeScraper.Services;

/// <summary>Fetches and parses HTML documents from remote URLs.</summary>
internal interface IFetcher
{
    /// <summary>Loads and parses the HTML document at the specified URL.</summary>
    Task<IHtmlDocument> LoadAsync(Uri url);
}
