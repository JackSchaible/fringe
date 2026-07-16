using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a user's vote on a show.</summary>
[DynamoDBTable("fringe")]
public class UserVoteRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

    /// <summary>Gets or sets the rank score (1 = most wanted).</summary>
    public int Score { get; set; }

    /// <summary>Gets or sets the ISO-8601 timestamp of the last update.</summary>
    public string UpdatedAt { get; set; } = null!;
}
