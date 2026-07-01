using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

[DynamoDBTable("fringe")]
public class UserRecord
{
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "PROFILE";

    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? GroupId { get; set; }
}
