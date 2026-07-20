using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>
/// DynamoDB persistence record describing one generated transfer-matrix version.
/// Lives at <c>TRANSFER_MATRIX#&lt;inputHash&gt; / METADATA</c>, alongside its
/// <see cref="TransferMatrixPairRecord"/> siblings in the same partition. Not indexed on
/// the entity-type GSI — matrices are always addressed directly by <see cref="InputHash"/>.
/// </summary>
[DynamoDBTable("fringe")]
public class TransferMatrixMetadataRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    /// <summary>Gets or sets the routing input hash that identifies this version.</summary>
    public string InputHash { get; set; } = null!;

    /// <summary>Gets or sets the number of venues that participated in this version.</summary>
    public int VenueCount { get; set; }

    /// <summary>Gets or sets the number of directional pair records this version contains.</summary>
    public int PairCount { get; set; }

    /// <summary>Gets or sets the ISO-8601 generation timestamp.</summary>
    public string GeneratedAt { get; set; } = null!;

    /// <summary>Gets or sets the routing provider that generated this version.</summary>
    public string Source { get; set; } = null!;

    /// <summary>
    /// Gets or sets the DynamoDB TTL expiry (epoch seconds), set only once this version is
    /// superseded by a newer active version. <see langword="null"/> while active or unreachable.
    /// </summary>
    public long? Ttl { get; set; }
}
