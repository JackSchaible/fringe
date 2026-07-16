using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a group.</summary>
[DynamoDBTable("fringe")]
public class GroupRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    /// <summary>Gets or sets the group identifier.</summary>
    public string GroupId { get; set; } = null!;

    /// <summary>Gets or sets the group display name.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Gets or sets the user ID of the group owner.</summary>
    public string OwnerId { get; set; } = null!;

    /// <summary>Gets or sets the invite code for joining the group.</summary>
    public string InviteCode { get; set; } = null!;

    /// <summary>Gets or sets the ISO-8601 creation timestamp.</summary>
    public string CreatedAt { get; set; } = null!;
}
