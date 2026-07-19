using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record mapping an invite code to a group.</summary>
[DynamoDBTable("fringe")]
public class InviteCodeRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    /// <summary>Gets or sets the group identifier this invite code belongs to.</summary>
    public string GroupId { get; set; } = null!;
}
