using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>
/// DynamoDB persistence record for one directional venue-pair transfer, carrying every
/// supported travel mode in a single item (not one item per mode). Lives at
/// <c>TRANSFER_MATRIX#&lt;inputHash&gt; / FROM#&lt;fromVenueNumber&gt;#TO#&lt;toVenueNumber&gt;</c>.
/// </summary>
[DynamoDBTable("fringe")]
public class TransferMatrixPairRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

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

    /// <summary>
    /// Gets or sets the DynamoDB TTL expiry (epoch seconds), set only once this version is
    /// superseded by a newer active version. <see langword="null"/> while active or unreachable.
    /// </summary>
    public long? Ttl { get; set; }
}
