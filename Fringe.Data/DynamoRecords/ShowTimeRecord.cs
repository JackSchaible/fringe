using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a single show performance.</summary>
[DynamoDBTable("fringe")]
public class ShowTimeRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

    /// <summary>Gets or sets the ISO-8601 UTC start time.</summary>
    public string DateTime { get; set; } = null!;

    /// <summary>Gets or sets the local performance time string.</summary>
    public string PerformanceTime { get; set; } = null!;

    /// <summary>Gets or sets the human-readable performance date string.</summary>
    public string PerformanceDate { get; set; } = null!;

    /// <summary>Gets or sets the presentation format (e.g. "In-Person").</summary>
    public string PresentationFormat { get; set; } = null!;

    /// <summary>Gets or sets a value indicating whether seating is reserved.</summary>
    public bool Reserved { get; set; }
}
