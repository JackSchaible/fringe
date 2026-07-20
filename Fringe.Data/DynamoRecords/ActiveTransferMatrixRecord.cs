using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>
/// DynamoDB persistence record for the singleton pointer at <c>CONFIG / ACTIVE_TRANSFER_MATRIX</c>
/// naming which <see cref="TransferMatrixMetadataRecord"/>/<see cref="TransferMatrixPairRecord"/>
/// partition (by <see cref="InputHash"/>) is currently live. Flipping this single item is the
/// entire "publish" step — a new version is never visible to readers until this record points at it.
/// </summary>
[DynamoDBTable("fringe")]
public class ActiveTransferMatrixRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = "CONFIG";

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "ACTIVE_TRANSFER_MATRIX";

    /// <summary>Gets or sets the currently active version's input hash.</summary>
    public string InputHash { get; set; } = null!;

    /// <summary>Gets or sets the ISO-8601 timestamp this version was promoted to active.</summary>
    public string PromotedAt { get; set; } = null!;
}
