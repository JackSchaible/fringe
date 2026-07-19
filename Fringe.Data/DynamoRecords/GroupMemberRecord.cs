using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a group member.</summary>
[DynamoDBTable("fringe")]
public class GroupMemberRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

    /// <summary>Gets or sets the user identifier.</summary>
    public string UserId { get; set; } = null!;

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>Gets or sets the email address.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Gets or sets the ISO-8601 timestamp when the user joined the group.</summary>
    public string JoinedAt { get; set; } = null!;
}
