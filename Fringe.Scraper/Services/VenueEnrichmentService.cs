using System.Text.Json;
using Fringe.Data;
using Fringe.Data.DynamoRecords;

namespace FringeScraper.Services;

/// <summary>
/// Enriches canonical venues with routable coordinates. Only venues the repository reports
/// as eligible (missing coordinates, or a changed routing-relevant address hash) are geocoded —
/// this is what keeps re-running the pipeline every night cheap once venues are stable.
/// <paramref name="requestDelay"/> is the pause between geocoding calls (defaults to a
/// rate-limit-friendly 1.5s for production use); tests pass <see cref="TimeSpan.Zero"/>.
/// </summary>
internal sealed class VenueEnrichmentService(FringeRepository repository, IGeocodingProvider provider, TimeSpan? requestDelay = null)
{
    private const string coordinateSource = "OpenRouteService";
    private static readonly TimeSpan defaultRequestDelay = TimeSpan.FromMilliseconds(1500);
    private readonly TimeSpan delay = requestDelay ?? defaultRequestDelay;

    /// <summary>Geocodes every venue currently eligible for (re-)geocoding.</summary>
    public async Task EnrichAsync()
    {
        List<VenueRecord> venues = await repository.GetVenuesNeedingGeocodingAsync().ConfigureAwait(false);
        if (venues.Count == 0)
        {
            ScraperLogger.Log("No venues need geocoding.");
            return;
        }

        ScraperLogger.Log($"Geocoding {venues.Count} venue(s)...");
        int succeeded = 0;
        int failed = 0;

        foreach (VenueRecord venue in venues)
        {
            try
            {
                GeocodeOutcome outcome = await provider.GeocodeAsync(venue.Address, venue.PostalCode).ConfigureAwait(false);
                if (!outcome.IsSuccess)
                {
                    failed++;
                    ScraperLogger.Log($"⚠️ Could not geocode venue {venue.VenueNumber} ({venue.Name}): {outcome.FailureReason}");
                    continue;
                }

                bool updated = await repository.UpdateVenueCoordinatesAsync(
                    venue.VenueNumber, outcome.Latitude, outcome.Longitude, coordinateSource, DateTime.UtcNow)
                    .ConfigureAwait(false);
                if (updated)
                {
                    succeeded++;
                }
            }
            catch (HttpRequestException ex)
            {
                failed++;
                ScraperLogger.Log($"⚠️ Error geocoding venue {venue.VenueNumber}: {ex.GetType().Name}: {ex.Message}");
            }
            catch (JsonException ex)
            {
                failed++;
                ScraperLogger.Log($"⚠️ Error geocoding venue {venue.VenueNumber}: {ex.GetType().Name}: {ex.Message}");
            }

            // Be polite to the (rate-limited, free-tier) provider between requests.
            await Task.Delay(delay).ConfigureAwait(false);
        }

        ScraperLogger.Log($"Geocoding complete: {succeeded} succeeded, {failed} failed.");
    }
}
