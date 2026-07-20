using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FringeScraper.Services;

/// <summary>
/// Geocodes addresses via the OpenRouteService (Pelias) search API. Coordinate data is
/// © OpenStreetMap contributors, routing/geocoding via openrouteservice.org — attribution
/// must be shown wherever these coordinates are surfaced (e.g. a future map view).
/// </summary>
internal sealed class OpenRouteServiceGeocodingProvider(string apiKey) : IGeocodingProvider
{
    private const string baseUrl = "https://api.openrouteservice.org/geocode/search";

    /// <summary>
    /// Minimum Pelias confidence (0-1) to accept a match. Below this, the result is treated
    /// as an ambiguous/low-confidence failure rather than a guess.
    /// </summary>
    private const double minimumConfidence = 0.6;

    private static readonly HttpClient http = new();
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc/>
    public async Task<GeocodeOutcome> GeocodeAsync(string address, string postalCode, CancellationToken cancellationToken = default)
    {
        string query = string.Create(CultureInfo.InvariantCulture, $"{address}, {postalCode}");
        var uri = new Uri(string.Create(CultureInfo.InvariantCulture,
            $"{baseUrl}?api_key={Uri.EscapeDataString(apiKey)}&text={Uri.EscapeDataString(query)}&size=1"));

        using HttpResponseMessage response = await http.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return GeocodeOutcome.Failed(string.Create(CultureInfo.InvariantCulture,
                $"OpenRouteService returned {(int)response.StatusCode} {response.ReasonPhrase}"));
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        GeocodeResponse? data = JsonSerializer.Deserialize<GeocodeResponse>(json, jsonOptions);

        GeocodeFeature? best = data?.Features?.FirstOrDefault();
        if (best?.Geometry?.Coordinates is not { Length: 2 } coordinates)
        {
            return GeocodeOutcome.Failed("No geocoding results found.");
        }

        double confidence = best.Properties?.Confidence ?? 0;
        if (confidence < minimumConfidence)
        {
            return GeocodeOutcome.Failed(string.Create(CultureInfo.InvariantCulture, $"Low confidence match ({confidence:F2})."));
        }

        // GeoJSON orders coordinates [longitude, latitude].
        return GeocodeOutcome.Succeeded(latitude: coordinates[1], longitude: coordinates[0]);
    }

    private sealed class GeocodeResponse
    {
        [JsonPropertyName("features")]
        public List<GeocodeFeature>? Features { get; set; }
    }

    private sealed class GeocodeFeature
    {
        [JsonPropertyName("geometry")]
        public GeocodeGeometry? Geometry { get; set; }

        [JsonPropertyName("properties")]
        public GeocodeProperties? Properties { get; set; }
    }

    private sealed class GeocodeGeometry
    {
        [JsonPropertyName("coordinates")]
        public double[]? Coordinates { get; set; }
    }

    private sealed class GeocodeProperties
    {
        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }
    }
}
