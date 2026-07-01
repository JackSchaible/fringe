using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

[DynamoDBTable("fringe")]
public class InviteCodeRecord
{
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = "METADATA";

    public string GroupId { get; set; } = null!;
}
