using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

[DynamoDBTable("fringe")]
public class GroupMemberRecord
{
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string JoinedAt { get; set; } = null!;
}
