namespace FringeScraper.Services;

/// <summary>Resolves a street address to routable coordinates.</summary>
internal interface IGeocodingProvider
{
    /// <summary>Attempts to geocode the given address and postal code.</summary>
    Task<GeocodeOutcome> GeocodeAsync(string address, string postalCode, CancellationToken cancellationToken = default);
}
