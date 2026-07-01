using Amazon.DynamoDBv2.DataModel;

namespace Fringe.Data.DynamoRecords;

[DynamoDBTable("fringe")]
public class ShowTimeRecord
{
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

    public string DateTime { get; set; } = null!;
    public string PerformanceTime { get; set; } = null!;
    public string PerformanceDate { get; set; } = null!;
    public string PresentationFormat { get; set; } = null!;
    public bool Reserved { get; set; }
}
