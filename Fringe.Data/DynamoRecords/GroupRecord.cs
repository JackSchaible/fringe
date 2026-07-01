using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

[DynamoDBTable("fringe")]
public class GroupRecord
{
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    public string GroupId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string OwnerId { get; set; } = null!;
    public string InviteCode { get; set; } = null!;
    public string CreatedAt { get; set; } = null!;
}
