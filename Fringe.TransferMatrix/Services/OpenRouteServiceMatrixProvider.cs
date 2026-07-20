using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fringe.Data.Models;

namespace FringeTransferMatrix.Services;

/// <summary>
/// Requests full NxN duration/distance matrices from the OpenRouteService Matrix API. Coordinate
/// data is © OpenStreetMap contributors, routing via openrouteservice.org — attribution must be
/// shown wherever these durations/distances are surfaced (e.g. schedule transfer messaging).
/// </summary>
internal sealed class OpenRouteServiceMatrixProvider(string apiKey) : IMatrixProvider
{
    private const string baseUrl = "https://api.openrouteservice.org/v2/matrix";

    private static readonly HttpClient http = new();
    private static readonly JsonSerializerOptions jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc/>
    public async Task<MatrixOutcome> GetMatrixAsync(TravelMode mode, IReadOnlyList<(int VenueNumber, double Latitude, double Longitude)> venues, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(venues);
        string profile = ProfileFor(mode);

        var body = new MatrixRequest
        {
            Locations = [.. venues.Select(v => new[] { v.Longitude, v.Latitude })],
            Metrics = ["duration", "distance"]
        };

        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/{profile}") { Content = content };
        request.Headers.Add("Authorization", apiKey);

        using HttpResponseMessage response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return MatrixOutcome.Failed(string.Create(CultureInfo.InvariantCulture,
                $"OpenRouteService returned {(int)response.StatusCode} {response.ReasonPhrase} for profile {profile}"));
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        MatrixResponse? data = JsonSerializer.Deserialize<MatrixResponse>(json, jsonOptions);

        return data?.Durations == null || data.Distances == null
            ? MatrixOutcome.Failed(string.Create(CultureInfo.InvariantCulture,
                $"OpenRouteService returned no matrix data for profile {profile}"))
            : MatrixOutcome.Succeeded(data.Durations, data.Distances);
    }

    private static string ProfileFor(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Walking => "foot-walking",
            TravelMode.Cycling => "cycling-regular",
            TravelMode.Driving => "driving-car",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private sealed class MatrixRequest
    {
        [JsonPropertyName("locations")]
        public List<double[]>? Locations { get; set; }

        [JsonPropertyName("metrics")]
        public List<string>? Metrics { get; set; }
    }

    private sealed class MatrixResponse
    {
        [JsonPropertyName("durations")]
        public List<List<double?>>? Durations { get; set; }

        [JsonPropertyName("distances")]
        public List<List<double?>>? Distances { get; set; }
    }
}
