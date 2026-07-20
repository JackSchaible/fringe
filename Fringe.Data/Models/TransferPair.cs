namespace Fringe.Data.Models;

/// <summary>Domain model for one directional venue-pair transfer across every supported travel mode.</summary>
public class TransferPair
{
    /// <summary>Gets or sets the origin venue number.</summary>
    public int FromVenueNumber { get; set; }

    /// <summary>Gets or sets the destination venue number.</summary>
    public int ToVenueNumber { get; set; }

    /// <summary>Gets or sets the walking duration in seconds.</summary>
    public double WalkingDurationSeconds { get; set; }

    /// <summary>Gets or sets the walking distance in meters.</summary>
    public double WalkingDistanceMeters { get; set; }

    /// <summary>Gets or sets the cycling duration in seconds.</summary>
    public double CyclingDurationSeconds { get; set; }

    /// <summary>Gets or sets the cycling distance in meters.</summary>
    public double CyclingDistanceMeters { get; set; }

    /// <summary>Gets or sets the driving duration in seconds.</summary>
    public double DrivingDurationSeconds { get; set; }

    /// <summary>Gets or sets the driving distance in meters.</summary>
    public double DrivingDistanceMeters { get; set; }

    /// <summary>Gets or sets the routing provider that produced this pair's values.</summary>
    public string Source { get; set; } = null!;
}
