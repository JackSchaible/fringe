using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

[DynamoDBTable("fringe")]
public class UserAvailabilityRecord
{
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

    public List<AvailabilityWindowData> Windows { get; set; } = [];
}

public class AvailabilityWindowData
{
    public string Start { get; set; } = null!;
    public string End { get; set; } = null!;
}
