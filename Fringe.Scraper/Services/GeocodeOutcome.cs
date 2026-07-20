namespace FringeScraper.Services;

/// <summary>
/// The result of a geocoding attempt. Ambiguous, missing, or low-confidence matches are
/// modeled as an explicit failure rather than a best-guess coordinate, so a bad match never
/// silently becomes a venue's location — <see cref="FailureReason"/> is left for a human to review.
/// </summary>
internal sealed class GeocodeOutcome
{
    /// <summary>Gets a value indicating whether a confident coordinate match was found.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>Gets the matched latitude. Only meaningful when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public double Latitude { get; private init; }

    /// <summary>Gets the matched longitude. Only meaningful when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public double Longitude { get; private init; }

    /// <summary>Gets the human-readable reason geocoding failed. Only set when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public string? FailureReason { get; private init; }

    /// <summary>Creates a successful outcome with the matched coordinates.</summary>
    public static GeocodeOutcome Succeeded(double latitude, double longitude)
    {
        return new GeocodeOutcome { IsSuccess = true, Latitude = latitude, Longitude = longitude };
    }

    /// <summary>Creates a failed outcome with a reason for review.</summary>
    public static GeocodeOutcome Failed(string reason)
    {
        return new GeocodeOutcome { IsSuccess = false, FailureReason = reason };
    }
}
