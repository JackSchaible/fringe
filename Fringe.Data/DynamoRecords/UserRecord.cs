using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a user profile.</summary>
[DynamoDBTable("fringe")]
public class UserRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "PROFILE";

    /// <summary>Gets or sets the email address.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>Gets or sets the identifier of the group the user belongs to, or <see langword="null"/>.</summary>
    public string? GroupId { get; set; }
}
