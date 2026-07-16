using Amazon.DynamoDBv2.DataModel;
using System.Collections.ObjectModel;

namespace Fringe.Data.DynamoRecords;

/// <summary>DynamoDB persistence record for a user's availability windows.</summary>
[DynamoDBTable("fringe")]
public class UserAvailabilityRecord
{
    /// <summary>Gets or sets the partition key.</summary>
    [DynamoDBHashKey("pk")]
    public string Pk { get; set; } = null!;

    /// <summary>Gets or sets the sort key.</summary>
    [DynamoDBRangeKey("sk")]
    public string Sk { get; set; } = null!;

    /// <summary>Gets the availability windows. The collection is mutable via <see cref="Collection{T}.Add"/>.</summary>
    public Collection<AvailabilityWindowData> Windows { get; private set; } = [];
}

/// <summary>Represents a single availability window with ISO-8601 start and end times.</summary>
public class AvailabilityWindowData
{
    /// <summary>Gets or sets the ISO-8601 start time.</summary>
    public string Start { get; set; } = null!;

    /// <summary>Gets or sets the ISO-8601 end time.</summary>
    public string End { get; set; } = null!;
}
